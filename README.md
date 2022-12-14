# Description

This mod was created for anyone who is disappointed by how portals work in Valheim.
It adds a new building piece called the `Portal Station` and an item named `Personal Teleportation Device`.

# Portal Stations

`Portal stations` are capabable of teleporting players from any station to all other ones.
However, they are more expensive to build than regular portals as they require `4` surtling cores, `20` stone and `20` Greydwarf eyes.

# Personal Teleportation Device

The `Personal Teleportation Device` item can be used to quickly teleport from any location to nearby portal stations and also allow to teleport back to the last known location.
They can be built at the forge (level 3+) with `3` surtling cores, `10` leather scraps and `10` iron nails and come with `2` quality upgrades.

Each teleportation will cost "fuel" and also adds some wear to it (can be repaired at a forge).

Each quality update will reduce fuel cost and increases the items durability so it can be used more often until it breaks.

# Configuration Options

Configuration options can be changed in `Valheim/BepInEx/config/com.undeadbits.valheimmods.portalstation.cfg`:

- **ignoreTeleportationRestrictions**  
   Configures whether vanilla teleportation restrictions will be ignored by portal stations and personal teleportation devices.

  Default value: `false`

- **fuelItemName**
  Configures the fuel item used by personal teleportation devices.
  Leave empty to disable fuel consumption.

  Default value: `"GreydwarfEye"`  
   Example values: `"Coins"`, `"Crystal"`, `"Resin"`  
   [Browse Item List](https://valheim-modding.github.io/Jotunn/data/objects/item-list.html)

- **teleportationDistancePerFuelItem**
  Configures the teleportation distance per fuel item.
  Default value: `1000`

- **additionalTeleportationDistancePerUpgrade**
  Configures the additional teleportation distance per device upgrade.
  Default value: `1000`

# Roadmap

- The current portal model is a placeholder, a new model is currently in development.  
  However, we will try to keep the current dimensions so it won't mess with your buildings (if placed inside).

You can find additional features and bugs on the roadmap [here](https://github.com/UndeadBits/valheim-portal-stations/issues).

# Attributions

- "Lower Arm Leather Armor" (https://skfb.ly/6ZCzx) by Thunder is licensed under Creative Commons Attribution (http://creativecommons.org/licenses/by/4.0/).
- "Ancient_Portal" (https://skfb.ly/6YMEr) by imitate is licensed under Creative Commons Attribution (http://creativecommons.org/licenses/by/4.0/).
