"""Table 2 (NAMRC 55): verify the app's beam solver against three references.

References, per case in cases.json:
  1. Closed form   - textbook formulas / statics + superposition, coded independently below.
  2. Stiffness     - Hermite-element direct stiffness method (test-only C# reference), from csharp_results.json.
  3. IndeterminateBeam 2.4.0 (Bonanno 2021, JOSE), the solver HoloMech ran in the cloud, run locally here.

Run (after `dotnet run -c Release -- cases.json csharp_results.json` or the built dll):
  python -I crosscheck.py cases.json csharp_results.json <out_dir>

Sign convention throughout (same as ARBeam.Mechanics and IndeterminateBeam): loads positive downward in
cases.json; reaction forces positive upward; reaction moments counter-clockwise positive; bending moment
sagging positive; deflection upward positive.
"""

import csv
import json
import math
import sys
import time
from pathlib import Path

import numpy as np
from scipy.integrate import quad
from scipy.optimize import minimize_scalar

from importlib.metadata import version as pkg_version
from indeterminatebeam import Beam, PointLoadV, Support, UDLV


# ---------------------------------------------------------------- closed form

def propped_udl_vmax(w, L, EI):
    """Propped cantilever (clamp at x=0, pin at x=L), full UDL: v(x) = w x^2 (3L^2 - 5Lx + 2x^2) / (48 EI)."""
    x = (15 - math.sqrt(33)) / 16 * L
    return w * x**2 * (3 * L**2 - 5 * L * x + 2 * x**2) / (48 * EI)


def ss_point_defl(P, a, L, EI, x):
    """Downward deflection of a simply supported beam, point load P at a (standard table formula)."""
    b = L - a
    if x <= a:
        return P * b * x * (L**2 - b**2 - x**2) / (6 * EI * L)
    xr = L - x
    return P * a * xr * (L**2 - a**2 - xr**2) / (6 * EI * L)


def maximise(f, lo, hi, n=2001):
    """Max of a smooth f on [lo, hi]: grid, then bounded refinement around the best grid point."""
    xs = np.linspace(lo, hi, n)
    ys = np.array([f(x) for x in xs])
    k = int(np.argmax(ys))
    a, b = xs[max(k - 1, 0)], xs[min(k + 1, n - 1)]
    r = minimize_scalar(lambda x: -f(x), bounds=(a, b), method="bounded", options={"xatol": 1e-12})
    return max(ys[k], -r.fun)


def closed_form(c, EI):
    """Reactions [(x, force, moment)], |M|max, |v|max for each case, from standard results."""
    cid, L = c["id"], c["L"]
    P = [p["P"] for p in c["point_loads"]]
    w = [u["w"] for u in c["udls"]]

    if cid == "C01":  # cantilever, end load: R = P, M_A = PL, v = PL^3/3EI
        p = P[0]
        return [(0, p, p * L)], p * L, p * L**3 / (3 * EI)
    if cid == "C02":  # SS central load: PL/4, PL^3/48EI
        p = P[0]
        return [(0, p / 2, 0), (L, p / 2, 0)], p * L / 4, p * L**3 / (48 * EI)
    if cid == "C03":  # SS load at a: R = Pb/L, Pa/L; M = Pab/L; v = Pc(L^2-c^2)^1.5/(9 sqrt3 L EI), c = min(a, b)
        p, a = P[0], c["point_loads"][0]["x"]
        b = L - a
        cc = min(a, b)
        return ([(0, p * b / L, 0), (L, p * a / L, 0)], p * a * b / L,
                p * cc * (L**2 - cc**2) ** 1.5 / (9 * math.sqrt(3) * L * EI))
    if cid == "C04":  # SS full UDL: wL/2, wL^2/8, 5wL^4/384EI
        q = w[0]
        return [(0, q * L / 2, 0), (L, q * L / 2, 0)], q * L**2 / 8, 5 * q * L**4 / (384 * EI)
    if cid == "C05":  # fixed-fixed central load: P/2, end moments PL/8, M = PL/8, v = PL^3/192EI
        p = P[0]
        return ([(0, p / 2, p * L / 8), (L, p / 2, -p * L / 8)], p * L / 8, p * L**3 / (192 * EI))
    if cid == "C06":  # propped cantilever UDL: 5wL/8, 3wL/8, M_A = wL^2/8
        q = w[0]
        return ([(0, 5 * q * L / 8, q * L**2 / 8), (L, 3 * q * L / 8, 0)], q * L**2 / 8,
                propped_udl_vmax(q, L, EI))
    if cid == "C07":  # overhang: span s, overhang a, tip load. R_A = -Pa/s, R_B = P(s+a)/s, M = Pa, tip v = Pa^2(s+a)/3EI
        p = P[0]
        s = c["supports"][1]["x"]
        a = L - s
        return ([(0, -p * a / s, 0), (s, p * (s + a) / s, 0)], p * a, p * a**2 * (s + a) / (3 * EI))
    if cid == "C08":  # two equal spans l, UDL: 3wl/8, 10wl/8, 3wl/8; M_B = -wl^2/8; each span = propped cantilever
        q, l = w[0], L / 2
        return ([(0, 3 * q * l / 8, 0), (l, 10 * q * l / 8, 0), (L, 3 * q * l / 8, 0)], q * l**2 / 8,
                propped_udl_vmax(q, l, EI))
    if cid == "C09":  # clamp at x=a0, pin at L, tip load P at x=0, UDL on span s = L - a0.
        # The clamp fixes the slope, so the overhang is a cantilever (tip v = P a0^3/3EI) and the span is a
        # propped cantilever (R_pin = 3ws/8, clamp share 5ws/8, fixed-end moment ws^2/8).
        p, q = P[0], w[0]
        a0 = c["supports"][0]["x"]
        s = L - a0
        reactions = [(a0, p + 5 * q * s / 8, q * s**2 / 8 - p * a0), (L, 3 * q * s / 8, 0)]
        m_abs = max(p * a0, q * s**2 / 8, 9 * q * s**2 / 128)
        v_abs = max(p * a0**3 / (3 * EI), propped_udl_vmax(q, s, EI))
        return reactions, m_abs, v_abs
    if cid == "C10":  # three equal spans l, UDL (three-moment equation): 0.4, 1.1, 1.1, 0.4 wl; M_B = M_C = -wl^2/10
        q, l = w[0], L / 3
        m0 = q * l**2 / 10
        reactions = [(0, 0.4 * q * l, 0), (l, 1.1 * q * l, 0), (2 * l, 1.1 * q * l, 0), (L, 0.4 * q * l, 0)]
        # End span = simply supported span with UDL and hogging moment m0 at its inner end (superposition).
        end = lambda x: (q * x * (l**3 - 2 * l * x**2 + x**3) / (24 * EI)
                         - m0 * x * (l**2 - x**2) / (6 * EI * l))
        mid = 5 * q * l**4 / (384 * EI) - m0 * l**2 / (8 * EI)
        return reactions, m0, max(maximise(end, 0, l), mid)
    if cid == "C11":  # simply supported, several loads: statics + superposition
        pls = [(p["x"], p["P"]) for p in c["point_loads"]]
        uds = [(u["a"], u["b"], u["w"]) for u in c["udls"]]
        total = sum(p for _, p in pls) + sum(q * (b - a) for a, b, q in uds)
        mom0 = sum(p * x for x, p in pls) + sum(q * (b - a) * (a + b) / 2 for a, b, q in uds)
        rb = mom0 / L
        ra = total - rb

        def moment(x):
            m = ra * x - sum(p * (x - a) for a, p in pls if x > a)
            for a, b, q in uds:
                if x > a:
                    m -= q * (min(x, b) - a) * (x - (a + min(x, b)) / 2)
            return m

        xs = np.unique(np.concatenate([np.linspace(0, L, 600001), [a for a, _ in pls]]))
        m_abs = max(abs(moment(x)) for x in xs)

        def defl(x):  # downward
            v = sum(ss_point_defl(p, a, L, EI, x) for a, p in pls)
            for a, b, q in uds:
                v += quad(lambda s: q * ss_point_defl(1.0, s, L, EI, x), a, b, points=[x] if a < x < b else None,
                          epsabs=0, epsrel=1e-13, limit=200)[0]
            return v

        return [(0, ra, 0), (L, rb, 0)], m_abs, maximise(defl, 0, L, 601)
    raise KeyError(cid)


# ---------------------------------------------------------------- IndeterminateBeam

def run_ib(c, E, I):
    L = c["L"]
    beam = Beam(L, E=E, I=I)  # G = inf by default: no shear deformation, same Euler-Bernoulli model
    sups = []
    for k, s in enumerate(c["supports"]):
        fixed_x = 1 if k == 0 else 0  # IndeterminateBeam needs one axial restraint; none carries load here
        sups.append(Support(s["x"], (fixed_x, 1, 1 if s["kind"] == "fixed" else 0)))
    beam.add_supports(*sups)
    for p in c["point_loads"]:
        beam.add_loads(PointLoadV(-p["P"], p["x"]))
    for u in c["udls"]:
        beam.add_loads(UDLV(-u["w"], (u["a"], u["b"])))

    t0 = time.perf_counter()
    beam.analyse()
    t_solve = time.perf_counter() - t0

    reactions = []
    for s in c["supports"]:
        rx, ry, rm = beam.get_reaction(s["x"])
        reactions.append((s["x"], ry, rm))

    # get_*(return_absmax=True) samples only 200 points; query a dense grid plus every key point instead.
    keys = [s["x"] for s in c["supports"]] + [p["x"] for p in c["point_loads"]]
    keys += [u["a"] for u in c["udls"]] + [u["b"] for u in c["udls"]]
    xs = sorted(set(np.round(np.concatenate([np.linspace(0, L, 8001), keys]), 12)))
    m = np.array(beam.get_bending_moment(*xs))
    v = np.array(beam.get_deflection(*xs))
    m_abs = float(np.max(np.abs(m)))
    k = int(np.argmax(np.abs(v)))
    lo, hi = xs[max(k - 1, 0)], xs[min(k + 1, len(xs) - 1)]
    r = minimize_scalar(lambda x: -abs(beam.get_deflection(x)), bounds=(lo, hi), method="bounded",
                        options={"xatol": 1e-12})
    v_abs = max(float(abs(v[k])), -r.fun)
    return {"reactions": reactions, "m_abs": m_abs, "v_abs": v_abs, "solve_s": t_solve,
            "absmax_200pt": {"M": beam.get_bending_moment(return_absmax=True),
                             "v": beam.get_deflection(return_absmax=True)}}


# ---------------------------------------------------------------- comparison

def rel_errors(test, ref, scale_f, scale_m):
    """Max relative error over reactions (force / scale_f, moment / scale_m), |M|max and |v|max."""
    errs = {}
    rt = {round(x, 9): (f, mo) for x, f, mo in test["reactions"]}
    worst_r = 0.0
    for x, f, mo in ref["reactions"]:
        ft, mt = rt[round(x, 9)]
        worst_r = max(worst_r, abs(ft - f) / scale_f, abs(mt - mo) / scale_m)
    errs["reactions"] = worst_r
    errs["M"] = abs(test["m_abs"] - ref["m_abs"]) / ref["m_abs"]
    errs["v"] = abs(test["v_abs"] - ref["v_abs"]) / ref["v_abs"]
    errs["max"] = max(errs.values())
    return errs


def main():
    here = Path(__file__).resolve().parent
    cases_path = Path(sys.argv[1]) if len(sys.argv) > 1 else here / "cases.json"
    cs_path = Path(sys.argv[2]) if len(sys.argv) > 2 else here / "csharp_results.json"
    out_dir = Path(sys.argv[3]) if len(sys.argv) > 3 else here / "out"
    out_dir.mkdir(parents=True, exist_ok=True)

    spec = json.loads(cases_path.read_text())
    E, I = spec["E"], spec["I"]
    EI = E * I
    cs = {r["id"]: r for r in json.loads(cs_path.read_text())["results"]}

    rows, raw = [], []
    for c in spec["cases"]:
        cid = c["id"]
        total = sum(p["P"] for p in c["point_loads"]) + sum(u["w"] * (u["b"] - u["a"]) for u in c["udls"])
        scale_f, scale_m = total, total * c["L"]

        cf_r, cf_m, cf_v = closed_form(c, EI)
        ref = {"reactions": cf_r, "m_abs": cf_m, "v_abs": cf_v}

        r = cs[cid]
        ours = {"reactions": [(q["x"], q["force"], q["moment"]) for q in r["reactions"]],
                "m_abs": abs(r["peak_moment"]["value"]), "v_abs": abs(r["peak_deflection"]["value"])}
        st = r["stiffness"]
        stiff = {"reactions": [(q["x"], q["force"], q["moment"]) for q in st["reactions"]]}

        ib = run_ib(c, E, I)

        e_cf = rel_errors(ours, ref, scale_f, scale_m)
        e_ib = rel_errors(ours, ib, scale_f, scale_m)
        e_ib_cf = rel_errors(ib, ref, scale_f, scale_m)
        st_r = max(max(abs(a[1] - b[1]) / scale_f, abs(a[2] - b[2]) / scale_m)
                   for a, b in zip(sorted(ours["reactions"]), sorted(stiff["reactions"])))
        st_v = st["max_nodal_deflection_diff"] / st["max_abs_nodal_deflection"]

        rows.append({
            "case": cid, "name": c["name"],
            "n_supports": len(c["supports"]),
            "n_clamps": sum(s["kind"] == "fixed" for s in c["supports"]),
            "n_point": len(c["point_loads"]), "n_udl": len(c["udls"]),
            "M_abs_Nm": ours["m_abs"], "v_abs_mm": ours["v_abs"] * 1e3,
            "M_abs_closed": cf_m, "v_abs_closed_mm": cf_v * 1e3,
            "err_vs_closed": e_cf["max"],
            "err_vs_stiffness": max(st_r, st_v),
            "err_vs_IB": e_ib["max"],
            "err_IB_vs_closed": e_ib_cf["max"],
            "IB_200pt_absmax_v_err": abs(ib["absmax_200pt"]["v"] - cf_v) / cf_v,
            "IB_solve_s": ib["solve_s"], "ours_solve_us_pc": r["solve_us_median_pc"],
        })
        raw.append({"case": cid, "closed_form": ref, "ours": ours, "stiffness": stiff, "indeterminatebeam": ib,
                    "errors": {"ours_vs_closed": e_cf, "ours_vs_IB": e_ib, "IB_vs_closed": e_ib_cf,
                               "ours_vs_stiffness": {"reactions": st_r, "nodal_v": st_v}}})
        print(f"{cid}: closed {e_cf['max']:.1e}  stiff {max(st_r, st_v):.1e}  IB {e_ib['max']:.1e}  "
              f"(IB vs closed {e_ib_cf['max']:.1e})")

    with open(out_dir / "table2_solver_verification.csv", "w", newline="", encoding="utf-8") as f:
        wr = csv.DictWriter(f, fieldnames=list(rows[0]))
        wr.writeheader()
        wr.writerows(rows)
    meta = {"E_Pa": E, "I_m4": I, "indeterminatebeam_version": pkg_version("indeterminatebeam"),
            "python": sys.version.split()[0], "numpy": np.__version__}
    (out_dir / "table2_raw.json").write_text(json.dumps({"meta": meta, "cases": raw}, indent=2, default=float))
    print("max over cases:",
          {k: f"{max(r[k] for r in rows):.1e}" for k in ("err_vs_closed", "err_vs_stiffness", "err_vs_IB",
                                                          "err_IB_vs_closed", "IB_200pt_absmax_v_err")})


if __name__ == "__main__":
    main()
