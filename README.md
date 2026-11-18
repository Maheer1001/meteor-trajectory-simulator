# Meteor Trajectory Simulator

A Unity simulation of a meteor falling towards Earth. You set the meteor's properties and starting position, then watch it fall through space, enter the atmosphere, and either burn up or hit the ground. If it hits, the simulator estimates the crater size and blast radii and shows where on Earth it landed.

> My A-Level Computer Science NEA (AQA), built in Unity with C#.

## What it simulates

- **Space stage:** gravitational acceleration towards Earth, integrated each physics step with **4th-order Runge-Kutta (RK4)**. The meteor's temperature changes by radiative heat loss to space (Stefan-Boltzmann).
- **Atmosphere stage** (from about 85 km altitude):
  - Air temperature and pressure that change with altitude across three layers
  - Drag, using a Reynolds-number-based drag coefficient correlation for a sphere
  - Heating and cooling from aerodynamic heating, convective cooling (Nusselt number) and radiative cooling
  - Mass loss (ablation), with the meteor's radius recalculated from its mass and density
  - Wind speed and direction set with sliders
  - The meteor disintegrates if its mass or radius falls below a threshold
- **Collision stage:**
  - Kinetic energy, crater radius, and impact radii for a set of overpressure levels, drawn as rings on an Earth map
  - The impact position is converted to latitude/longitude (allowing for Earth's rotation, axial tilt and precession) and reverse-geocoded to a place name, or "Location is in the sea"
  - A graph of distance travelled over time, sampled every 1, 10 or 30 minutes depending on how long the fall took

## Inputs and controls

- **Variable input screen:** mass (5,000 to 60,000 kg), density (1,000 to 8,000 kg/m³), specific heat capacity (10 to 100), surface condition factor (0 to 1), and the X/Y/Z starting coordinates (-50 to -1 or 1 to 50). Values outside a range are clamped.
- **Time-scale slider** speeds the simulation up or slows it down.
- **Pause / play** saves the simulation state on a stack and restores it.
- **Collision map:** mouse wheel to zoom, arrow keys / WASD to pan.
- The main menu has Start, Continue, Restart, Exit, and a More Information screen that links to background reading (RK4, drag coefficients, overpressure levels).

## Running it

1. Install **Unity 2022.3.7f1** (any 2022.3 LTS should work) with Unity Hub.
2. In Unity Hub, choose **Add project from disk** and select this folder. Unity rebuilds `Library/` the first time it opens, which takes a few minutes.
3. Open `Assets/Scenes/MainMenu.unity` and press Play.

Scene order: `MainMenu` → `VariableInput` → `SpaceSimulation` → `AtmosphereReached` → `EarthCollision`.

### Reverse geocoding API key

The impact location lookup uses the [geocode.maps.co](https://geocode.maps.co/) reverse-geocoding API. The key has been removed from this repo. To turn the lookup on, get a free key and replace `YOUR_GEOCODE_MAPS_CO_API_KEY` in `Assets/Scripts/Collision/CoordinateCalculator.cs`. Everything else in the simulation works without it.

## Project layout

```
Assets/Scripts/Space/        RK4 gravity and space heat loss (Meteor_Force.cs)
Assets/Scripts/Atmosphere/   Drag, heating, ablation, wind (AtmosphereForce.cs)
Assets/Scripts/Collision/    Crater/blast radii, lat/long + geocoding, graph, map zoom
Assets/Scripts/Inputs/       Range-checked user inputs
Assets/Scripts/Main menu/    Scene navigation and info links
Assets/Scenes/               The six scenes
```
