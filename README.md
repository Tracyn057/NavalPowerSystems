What started as an attempt at a proof of concept became something much, much more.

With the assistance of the Modular Assemblies Framework, Space Engineers naval vessels can now have a fully functioning drivetrain. Well, mostly functioning.
The mostly functioning part means that this is considered an Alpha release, and as such, there are still some features that are not fully implemented, and some bugs that need to be fixed. However, the core functionality is there, and it is possible to create a working drivetrain.

With the introduction of Gas Turbine and Diesel engines, it also necessitated the addition of a new resource system to gather and refine the fuel sources.


There's quite a few moving parts here and this writeup here is about the only guide. So, learning curve might be a bit on the hard side but I'll try to cover what I can.
Also, this is still a heavily WIP system. It's missing a lot. It's probably buggy. I have no idea if multiplayer works at all.


The Oil System:
This is a whole new system that was added because I wasn't happy with any of the other oil mods on the workshop. It is a fully self contained system that allows you to gather crude oil, refine it into various fuels, and then use those fuels in your engines.

Oil "spawns" in the world as a "deep resource". There is no oilsand to mine. Instead, you use an LCD with the new program "Oil Deposit Scanner." With this, you zoom around looking for concentrations of oil, which will appear as a heatmap of quality.
![Alt text](https://github.com/Tracyn057/NavalPowerSystems/blob/main/Images/Oil%20Heatmap.png)

Oil will only appear:
- On planets with an atmosphere
- In water or low altitude areas

If oil does spawn on land, it will be a much less concentrated deposit. Ideal deposits are always at sea.
To begin gathering the oil, you need to build a drill rig. This consists of the Rig structure itself, a drill head at ground level and drill rods connecting them.
As of this writing, the drill components are all reused vanilla blocks. New models will come at a future time.

![Alt text](https://github.com/Tracyn057/NavalPowerSystems/blob/main/Images/Drill%20Head.png)
![Alt text](https://github.com/Tracyn057/NavalPowerSystems/blob/main/Images/Rig%20Data.png)

From there, you will need to use the conveyor ports to connect the drill rig to holding tanks for the extracted crude oil. After that, there's two more processes to use. The first step to refining fuel is Heavy Fuel Oil. This fuel has no use in the current state of the mod. To make Fuel Oil, connect your crude oil tanks to the Refinery Crude Input block. On the other side of that goes the Oil Cracker.
The Oil Cracker will then start the conversion process and output Fuel Oil. This HFO needs to be stored in its own tanks.

The next step is to take the HFO and refine it into Diesel. Similar to the Oil Cracker, the HFO goes into the Refinery Fuel Oil Input that is then connected to the Fuel Refinery. The resulting Diesel will, again, need its own storage tanks.

The refining process is not very efficient, and you will lose a significant amount of the original Crude Oil. Also, each resource has mass.
This means that you will need to have a way to transport the oil and fuels around. Perfect chance to build a real oil tanker.


The Drivetrain:

This is the main reason I started this mod. I never liked the idea of slapping down a generator on one end of the ship and throwing a few thrusters all over the place. With this, an engineering space is required.

Core components:
- Engines: The power source for the drivetrain. They consume fuel and produce torque.
- Gearboxes: This will take the output of the engine(s) and convert it to a usable form for the propellers. I've also put the reverse gear controls here.
- Driveshafts: I hope you know what these do.
- Propellers: Also these.

The system is very much designed for a specific flow, while still being relatively modular. Two engines can connect to each Main Reduction Gear, with a single driveshaft output. More configurations will come later.
![Alt text](https://github.com/Tracyn057/NavalPowerSystems/blob/main/Images/Gearbox1.png)
![Alt text](https://github.com/Tracyn057/NavalPowerSystems/blob/main/Images/Gearbox2.png)

The new Gas Turbines have a specific output port.
![Alt text](https://github.com/Tracyn057/NavalPowerSystems/blob/main/Images/TurbineOutput.png)

From the gearbox, you need driveshafts to connect to your propeller. There's a ton of different driveshafts to choose from even now. 

The other feature that I've included in this mod is the Rudder. Currently only the one variant at the moment. This block serves multiple purposes. It's a gyro with integrated, always active align to gravity. Ships generally tend to like to stay upright after all. The other use for the block is steering (obviously). You can use the A/D keys to turn your ship. Note: Due to water resistance, your ship will generally want to travel in a straight line and center itself to the direction of travel.

