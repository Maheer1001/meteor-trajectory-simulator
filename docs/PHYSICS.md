# Physics model

This document describes the physics in `Assets/Scripts/Core` (assembly `MTS.Core`): the models, equations, units, assumptions and sources. Everything is SI (m, kg, s, K, Pa, J) and double precision. The core is plain C# with no Unity dependency, so it can be unit-tested (see `Assets/Tests/EditMode`).

## 1. Frame, state and integration

- **Frame.** Earth-centred, inertial (non-rotating). `+Y` is the Earth's rotation axis (Unity's "up"), and the prime meridian lies along `+X` at `t = 0`. Earth-fixed coordinates are `(Xe, Ye, Ze) = (x, -z, y)`. The Earth is a sphere of radius `R = 6 371 000 m`, and altitude is `h = |r| - R`. The atmosphere is not modelled as co-rotating with the Earth.
- **State** (`MeteorState`): time, position `r`, velocity `v`, mass `m`, surface-layer temperature `T`, distance travelled, plus the fragmentation state (broken flag, break-up diameter `L0`, pancake diameter `L`, `dL/dt`).
- **Equations of motion** (all vectors):

  ```
  dr/dt = v
  dv/dt = -mu r / |r|^3  -  (1/2) rho_a Cd A |v_rel| v_rel / m
  v_rel = v - w(r)          (velocity relative to the air; w is the wind vector)
  ```

  `mu = 3.986004418e14 m^3 s^-2`. Gravity is a central vector. Drag always points opposite to the velocity relative to the air.
- **Integrator.** Classical 4th-order Runge-Kutta (`Rk4Integrator`) on the full vector state `(r, v, L, dL/dt)`. Mass and temperature are updated after each RK4 step (operator splitting) by the implicit heating/ablation step in Section 4.
- **Fixed steps.** The step size depends only on altitude: 1 s above 10 000 km, 0.25 s above 1000 km, 0.05 s above 200 km, and 0.01 s below 200 km. The time-scale slider never changes the step size. It sets how many simulated seconds pass per physics tick (`slider / 60` s per tick, as before), and the runner takes as many fixed sub-steps as that needs, carrying leftover time forward. This makes results independent of the time-scale setting (this is tested). There is a cap of 20 000 sub-steps per tick. If the cap is reached, the excess time is dropped and a warning is logged once.
- **Safety.** After every step the state is checked for NaN/Infinity. A bad value ends the run with `EndReason.Error`, keeps the last good state, and logs both states. It never hangs.
- **End reasons** (`SimulationResult.Reason`):
  - `Impact`: reached `h = 0`. The state is linearly interpolated to the surface within the last step.
  - `Airburst`: the fragment cloud spread to `L >= f_p L0` with `f_p = 7`.
  - `Disintegrated`: ablated away (`m <= 1 kg` or equivalent radius `<= 0.1 m`, the NEA's thresholds).
  - `Error`: a non-finite value appeared.
  - `Timeout`: more than 30 simulated days.

## 2. Start modes

- **Classic NEA** (default): released from rest at `r0 = (X, Y, Z) x 4050 km` (the input screen's coordinate unit), exactly as in the original design. It now falls along the true radial line and accelerates under the central field.
- **Realistic entry**: same start point, but on an inbound two-body (Keplerian) path chosen so it reaches the entry interface (120 km) at a set speed and flight-path angle. The defaults are 17 km/s and 45 degrees, the EIEP defaults. Energy and angular momentum give the starting speed `v0^2 = ve^2 - 2 mu (1/re - 1/r0)` and angle `cos(gamma0) = re ve cos(gamma) / (r0 v0)`. The tangential direction is local east. If the requested entry cannot be reached from that start point, the meteor falls from rest and a warning is logged.
- **Switching modes**, until a toggle is added to the screens, has two options:
  - the `realisticEntryMode` / `entrySpeed` / `entryAngleDegrees` fields on the space-scene `Meteor_Force` component;
  - the command-line arguments `-mtsMode realistic`, `-mtsEntrySpeed 17000` and `-mtsEntryAngle 45`.
- **Stages.** The space stage runs until `h <= 120 km` (entry interface). The atmosphere stage runs from there until an end event. One physics model covers both stages; the stage only decides which screen is shown.

## 3. Atmosphere: U.S. Standard Atmosphere 1976

`Atmosphere.Sample(h)`:

- **0 to 86 km:** the seven USSA76 layers, defined in geopotential altitude `H = r0 z / (r0 + z)` with `r0 = 6 356 766 m`:

  | Base H (km') | T_b (K) | Lapse (K/km') |
  | ---: | ---: | ---: |
  | 0 | 288.15 | -6.5 |
  | 11 | 216.65 | 0 |
  | 20 | 216.65 | +1.0 |
  | 32 | 228.65 | +2.8 |
  | 47 | 270.65 | 0 |
  | 51 | 270.65 | -2.8 |
  | 71 | 214.65 | -2.0 |

  Pressure follows the hydrostatic solutions: `P = Pb (Tb / T)^(g0 M / (R* L))` in gradient layers, and `P = Pb exp(-g0 M (H - Hb) / (R* Tb))` in isothermal layers. The constants are `g0 = 9.80665`, `M = 0.0289644 kg/mol` and `R* = 8.31432 J/(mol K)`. Base pressures are derived from 101 325 Pa so the layers join exactly. Density is `rho = P / (R T)` with `R = 287.053 J/(kg K)`. Below 86 km the temperature returned is the molecular-scale temperature, which differs from the kinetic temperature by less than 0.05 %.
- **86 to 1000 km:** log-linear interpolation of the USSA76 tabulated `P` and `rho`, with linear interpolation of `T`. Above 1000 km the last segment's exponential decay is extrapolated, so density smoothly tends to 0. Above 1000 km altitude the drag force is skipped as a speed-up (density there is below 4e-15 kg/m^3).
- **Derived quantities:**
  - speed of sound `a = sqrt(1.4 R T)`;
  - Sutherland viscosity `mu = 1.458e-6 T^1.5 / (T + 110.4)`;
  - mean free path `lambda = M / (sqrt(2) pi d^2 N_A rho)` with `d = 3.65e-10 m`.
- **Tested** against the USSA76 layer-base values at 0, 11, 20, 32, 47, 51, 71 and 84.852 km' and the geometric values at 11, 86 and 100 km.

## 4. Drag, heating and ablation

### Drag coefficient

Meteors fly hypersonically for most of their path, so the old subsonic Reynolds-number correlation for spheres (Morrison 2016) does not apply. The drag coefficient is `Cd(Mach, Kn)`:

- **Continuum flow:** sphere data versus Mach number (Hoerner 1965; Bailey and Hiatt 1972). Cd is 0.47 subsonic, 0.58 at M 0.8, 0.80 at M 1.0, 0.95 at M 1.2, 1.00 at M 1.5, 0.98 at M 2, 0.95 at M 3, and 0.92 from M 5 upwards (hypersonic).
- **Free-molecular flow** (`Kn = lambda / D > 10`): `Cd = 2`.
- **Transition:** a smoothstep in `log10 Kn` between `Kn = 0.01` and `Kn = 10`.

### Cross-section

- **Intact body:** `A = S_f pi r^2`. Here `r = (3m / (4 pi rho_i))^(1/3)` is the equal-volume radius, and the shape factor `S_f = 1 + 0.24 SCF` maps the input screen's shape-complexity factor from a sphere (0) to a cube's average cross-section (1).
- **Broken body:** `A = pi L^2 / 4`, where `L` is the pancake diameter.

### Heating

Single-body meteor theory with a lumped surface layer (`Heating`):

```
P_in  = (1/2) Lambda A rho_a |v_rel|^3   +   eps S0 A          (aerodynamic heating + sunlight)
P_rad = eps sigma S (T^4 - T_env^4),  S = 4A
m_h c dT/dt = P_in - P_rad,  m_h = min(m, rho_i S delta),  delta = 1 cm
```

- **Constants:**
  - heat-transfer coefficient `Lambda = 0.1`;
  - emissivity `eps = 0.7 + 0.3 SCF`, clamped to [0, 1];
  - solar constant `S0 = 1361 W/m^2`;
  - Stefan-Boltzmann constant `sigma = 5.670e-8 W m^-2 K^-4`;
  - environment temperature `T_env`: the air temperature below 86 km, the CMB (2.725 K) above;
  - `c` is the user's SHC input (now 400 to 1500 J/(kg K); iron is about 450, stony 700 to 1200).
- **Stable update.** The temperature equation is solved with a backward-Euler (implicit) step: a Newton iteration safeguarded by bisection, with a guaranteed bracket. It is unconditionally stable for the stiff `T^4` term and cannot go below 0 K. The old explicit update overshot to negative temperatures for light meteors and produced NaN.
- **Ablation.** Once `T` reaches the ablation temperature `T_abl`, the surface is held there and the surplus energy removes mass:

  ```
  dm/dt = -(P_in - P_rad(T_abl)) / Q*     which is about   -Lambda A rho_a v^3 / (2 Q*)
  ```

  This is the classical single-body ablation equation (Bronshten 1983; Ceplecha et al. 1998). With `Cd` about 1 it gives an ablation coefficient `sigma_abl = Lambda / (Cd Q*)` of about 0.0125 s^2/km^2, typical of ordinary-chondrite fireballs. Mass is clamped at 0 or above. The radius is recomputed from mass and density.
- **Initial temperature.** The radiative-equilibrium temperature of a fast-rotating grey sphere at 1 AU is `T0 = (S0 / (4 sigma) + T_cmb^4)^(1/4)`, about 278 K. It is independent of emissivity when absorptivity equals emissivity. This replaces the old formula, which gave about 4 K. In space the same energy balance keeps the meteor at that temperature.

### Material

`MeteorMaterial` holds everything about composition. By default a "custom" material is built from the inputs (density, SHC, SCF), and the parameters the user does not enter are chosen by density:

| | Stony (< 6000 kg/m^3) | Iron (>= 6000 kg/m^3) |
| --- | ---: | ---: |
| Q* (heat of ablation) | 8 MJ/kg | 6 MJ/kg |
| T_abl | 2000 K | 1811 K (melting point of iron) |
| Lambda | 0.1 | 0.1 |
| Strength Y_i | EIEP Eq. 9 (below) | EIEP Eq. 9 (below) |

**Presets** (`MeteorMaterialPresets`, chosen in the Space stage's launch panel; a preset replaces the density and SHC inputs). Rounded representative bulk values (densities: Britt and Consolmagno 2003; strengths: Popova et al. 2011; Q* and T_abl as used in fireball modelling, Ceplecha and ReVelle 2005):

| Preset | rho (kg/m^3) | c (J/(kg K)) | Q* (MJ/kg) | T_abl (K) | epsilon | Y_i (Pa) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Stony (ordinary chondrite) | 3300 | 750 | 8 | 2000 | 0.90 | 1e6 |
| Carbonaceous chondrite | 2200 | 900 | 5 | 1700 | 0.95 | 2e5 |
| Stony-iron (pallasite) | 4800 | 600 | 7 | 1900 | 0.85 | 1e7 |
| Iron (Fe-Ni) | 7800 | 450 | 6 | 1811 | 0.80 | 5e7 |

**Composition mix** (`MeteorMaterialPresets.Mix`, by mass fraction `w_i`): density by volume `1/rho = sum w_i / rho_i`; c, Q*, T_abl, epsilon and Lambda as mass-weighted means; strength as the weighted geometric mean `exp(sum w_i ln Y_i)`.

### Flight telemetry (display only)

`SimulationRunner.Telemetry` derives display quantities from the state; nothing feeds back into the physics. Mach `= v_rel / a`, ram pressure `q = 0.5 rho_a v_rel^2`, non-gravitational deceleration over the last step in g, mass-loss rate, and the visible radiant power `I = tau * 0.5 * |dm/dt| * v_rel^2` with luminous efficiency `tau = 0.01`. Absolute magnitude `M = -2.5 log10(I / 1500 W)` (zero-magnitude meteor power, Ceplecha et al. 1998), shown as +10 when not glowing.

## 5. Fragmentation and airburst (Collins et al. 2005, the EIEP "pancake" model)

- **Strength:** `log10 Y_i = 2.107 + 0.0624 sqrt(rho_i)` Pa (Eq. 9). For example, 3.35e5 Pa at 3000 kg/m^3 and 4.2e7 Pa at 7800 kg/m^3.
- **Break-up** when the ram pressure `rho_a v_rel^2` exceeds `Y_i` (Eq. 10). For example, a 3000 kg/m^3 body at 4 km/s breaks up at about 27 to 28 km.
- **Spreading:** after break-up the debris cloud's diameter grows as `d^2 L / dt^2 = Cd rho_a v^2 / (rho_i L)` (Eq. 13). This is integrated in time with RK4 together with the trajectory, using USSA76, instead of EIEP's analytic solution for an exponential atmosphere.
- **Airburst** when `L >= f_p L0` with `f_p = 7` (EIEP's choice). The cloud deposits essentially all of its kinetic energy between break-up and that point, most of it just before, so the blast uses the kinetic energy at break-up, released at the burst altitude. If the cloud reaches the ground first, it is a ground impact with the swarm's speed.
- **Check:** a Tunguska-like 75 m stony body (2000 kg/m^3, 17 km/s, 45 degrees) breaks up at about 61 km and bursts at about 4 km with about 15 Mt. EIEP gives about 66 km and 5 km using an exponential atmosphere.

## 6. Impact effects (Collins, Melosh and Marcus 2005)

`ImpactModel`. The target is sedimentary rock (`rho_t = 2500 kg/m^3`), with `g = 9.81 m/s^2`.

- **Energy:** `E = (1/2) m v^2`, also reported in kt TNT (`1 kt = 4.184e12 J`).
- **Transient crater** (Eq. 21): `D_tc = 1.161 (rho_i / rho_t)^(1/3) L^0.78 v^0.44 g^-0.22 sin(theta)^(1/3)`. Here `L` is the impactor diameter (equal-volume sphere of the remaining mass), `v` the impact speed and `theta` the angle to the horizontal. The transient depth is `D_tc / (2 sqrt 2)`.
- **Final crater:**
  - simple: `D_fr = 1.25 D_tc` (Eq. 22);
  - complex, if `D_tc > 2.56 km`: `D_fr = 1.17 D_tc^1.13 / D_c^0.13` in km, with `D_c = 3.2 km` (Eq. 27).
  - The depth is about `0.2 D_fr` (simple) or `0.4 D_fr^0.3` km (complex, Eq. 28).
- **Regime.** Below 3 km/s the result is flagged as a penetration pit / meteorite fall: the hypervelocity scaling is outside its range, so the crater is only indicative.
- **Surface-burst overpressure** (Eq. 54): `p = (p_x r_x / (4 r1)) (1 + 3 (r_x / r1)^1.3)`, with `p_x = 75 kPa`, `r_x = 290 m` and `r1 = r / E_kt^(1/3)`.
- **Airburst overpressure** (Eqs. 55 to 58), with scaled burst altitude `z_b1 = z_b / E_kt^(1/3)`:
  - inside the regular-reflection region: `p = p0 exp(-beta r1)`, with `p0 = 3.14e11 z_b1^-2.6` and `beta = 34.87 z_b1^-1.73`;
  - beyond the Mach-stem radius `r_m1 = 550 z_b1 / (1.2 (550 - z_b1))`, Eq. 54 applies with `r_x = 289 + 0.65 z_b1`;
  - there is no Mach region for `z_b1 >= 550 m`.
  - The fits come from nuclear-test data at modest scaled heights. Very high, small bursts (scaled heights of tens of km) are an extrapolation, and the model predicts almost no ground overpressure for them.
- **Blast radii.** For each of the nine rings on the results screen (0.04, 0.15, 0.7, 2, 3, 5, 7, 10 and 29 psi), the radius is the largest ground range at which `p >= level`. It is found by a logarithmic scan followed by bisection.
- **Tested against the paper's worked examples:**

  | Case | Paper | Model |
  | --- | --- | --- |
  | 40 m iron at about 10 km/s (Barringer) | E = 1.3e16 J, D = 1.2 km simple, 0.004 bar at 200 km | 1.18 km |
  | 1.75 km stony at 20 km/s (Ries) | 23.7 km complex, 0.80 bar at 200 km | 23.8 km |
  | 18 km at 20 km/s (Chicxulub) | 186 km | 186 km |
  | Glasstone and Dolan Table 4 distances | | within 8 % |
  | 1 t TNT surface burst | | 0.04 psi at about 2.0 km |

- **High-altitude airbursts.** The EIEP airburst fit (Eqs. 55-58) is calibrated on nuclear tests at modest scaled heights and gives almost nothing on the ground for small, high bursts (Chelyabinsk: 157 Pa below the burst, no damage rings). `ImpactModel.PeakOverpressure` therefore takes the larger of the EIEP value and a point-source model (`HighAltitudeAirburstOverpressure`):
  1. free-air blast of yield E = the Eq. 54 surface-burst curve at E/2 (Glasstone and Dolan 1977, sec. 3.34);
  2. Sachs scaling to the ambient pressure at the burst altitude, using the slant range from the burst (sec. 3.64-3.66);
  3. downward weak-shock propagation with amplitude growth `sqrt(rho_g c_g / (rho_b c_b))` (geometric acoustics, Pierce 1989); refraction, absorption and nonlinear attenuation are ignored;
  4. ground reflection factor 2.

  Chelyabinsk (about 500 kt at about 30 km) now gives about 1.6 kPa at ground zero and the 0.04 psi (window damage) ring at about 117 km, close to the observed window breakage up to about 120 km (Brown et al. 2013; Popova et al. 2013). Tunguska-like low bursts are unchanged below the burst. Because refraction and absorption are ignored, the outer (weakest) rings of mid-altitude bursts are larger than with the EIEP fit alone (1 Mt at 8 km: 0.04 psi ring 105 km to 251 km), so treat those outer radii as upper estimates.
- **Old model.** The old code computed an "explosive energy" from a drag power divided by a hard-coded wind speed. Its rings were thousands of km across (about 23 000 km for 0.04 psi), and its crater formula was not dimensionally consistent.

## 7. Impact location

The end position is rotated into the Earth-fixed frame using the elapsed time and Earth's sidereal rate (`7.2921159e-5 rad/s`, prime meridian on `+X` at `t = 0`). Then `lat = asin(Ze / |r|)` and `lon = wrap(atan2(Ye, Xe) - omega t)`, both in degrees. The location is reverse-geocoded with geocode.maps.co: culture-invariant URL, `UnityWebRequest`, cancelled when the scene unloads. The results map (`MapProjection` in `Assets/Scripts/Results`) is an equirectangular texture with an explicit, unit-tested lat/lon to map-position function. Damage rings are geodesic: each ring point is the destination point at the ring's ground range along bearings 0-360 degrees on a spherical Earth, so rings show the correct 1/cos(latitude) east-west stretch, and pole and antimeridian crossings are handled.

## 8. Simulation session

`SimulationSession.Instance` is the single owner of the committed inputs, the runner (state and results), pause state and wind. It survives scene loads.

- `Reset()` runs on Restart, on going back to the input screen, and before each new run.
- Pausing only stops the runner, so there is no state stack to save and restore.
- The scene MonoBehaviours (`Meteor_Force`, `Atmosphere_Force`, `MainMenuButton`), the HUD (`Assets/Scripts/UI`) and the results screen (`Assets/Scripts/Results`) only read the session's state; the HUD writes settings (mode, entry speed and angle, material, wind, time warp) back through the session.
- The results screen's charts are drawn from decimated time series recorded by the runner (at most 1024 samples each): the whole flight plus a finer series below 120 km, recording time, distance, altitude, speed, mass, temperature, Mach and ram pressure.

## 9. Main simplifications

- Spherical, non-rotating Earth, and an atmosphere at rest apart from the user's wind. The wind is horizontal: the speed comes from the wind slider, the direction is an azimuth fraction `f` (angle `f x pi` from local east towards north), set with the HUD's wind compass, and it applies below 86 km.
- Single body until break-up, then one pancake cloud. No further fragment cascades and no lift.
- Lumped surface-layer temperature (1 cm skin). Internal heat conduction is not modelled.
- Earth's shadow is ignored for solar heating.

## References

- NOAA, NASA, USAF (1976). *U.S. Standard Atmosphere, 1976*. NOAA-S/T 76-1562.
- Collins G. S., Melosh H. J., Marcus R. A. (2005). Earth Impact Effects Program: a Web-based computer program for calculating the regional environmental consequences of a meteoroid impact on Earth. *Meteoritics & Planetary Science* 40(6), 817-840.
- Chyba C. F., Thomas P. J., Zahnle K. J. (1993). The 1908 Tunguska explosion: atmospheric disruption of a stony asteroid. *Nature* 361, 40-44.
- Ceplecha Z. et al. (1998). Meteor phenomena and bodies. *Space Science Reviews* 84, 327-471.
- Bronshten V. A. (1983). *Physics of Meteoric Phenomena*. Reidel.
- Campbell-Brown M. D., Koschny D. (2004). Model of the ablation of faint meteors. *Astronomy & Astrophysics* 418, 751-758.
- Glasstone S., Dolan P. J. (1977). *The Effects of Nuclear Weapons*, 3rd ed.
- Brown P. G. et al. (2013). A 500-kiloton airburst over Chelyabinsk and an enhanced hazard from small impactors. *Nature* 503, 238-241.
- Popova O. P. et al. (2013). Chelyabinsk airburst, damage assessment, meteorite recovery, and characterization. *Science* 342, 1069-1073.
- Popova O. et al. (2011). Very low strengths of interplanetary meteoroids and small asteroids. *Meteoritics & Planetary Science* 46(10), 1525-1550.
- Britt D. T., Consolmagno G. J. (2003). Stony meteorite porosities and densities. *Meteoritics & Planetary Science* 38(8), 1161-1180.
- Ceplecha Z., ReVelle D. O. (2005). Fragmentation model of meteoroid motion, mass loss, and radiation in the atmosphere. *Meteoritics & Planetary Science* 40(1), 35-54.
- Pierce A. D. (1989). *Acoustics: An Introduction to Its Physical Principles and Applications*. Acoustical Society of America.
- Hoerner S. F. (1965). *Fluid-Dynamic Drag*. Bailey A. B., Hiatt J. (1972). Sphere drag coefficients for a broad range of Mach and Reynolds numbers. *AIAA Journal* 10(11).
