The IFMS (Integrated Flight Management System) is a collection of props
designed to work with each other.  It consists of an MFD, a Terminal,
a Main Computer Unit, and some additional accessory props.

The idea is that there must be one Main Computer Unit, the two Computer
Mode Select buttons, one or more Terminals, one or more MFDs, and
other accessory props as desired.

The MFDs are independent data display systems.  Some of the pages have more
than one configuration option, selectable by pressing the page button
(eg, LAUNCH has two pages - press the LAUNCH button repeatedly to select which
page is active).

The Terminals are data input systems.  They are used for various planning tasks,
such as entering launch altitude and inclination, or plotting maneuver nodes.
Multiple Terminals may be installed, but the last one used will override settings
made on a separate terminal.

The Main Computer Unit provides a prop that the IFMS can use for various TRIGGER_EVENT
triggers.  Exactly one MCU needs to be installed in the IVA.  Multiples will not harm
the IVA, but it will cause extra overhead because of redundant tasks being executed.
The MCU Mode Select buttons MAS_IFMS_pb_F03_MechJeb_Select and MAS_IFMS_pb_F03_MAS_Select
are used to switch between MechJeb and MAS pilots.  They
are intended to be inserted into the lower recessed squares on the MCU.  MAS_IFMS_pb_F03_Reset
belongs in the upper recess.  It is used to reset the MCU to default configurations.
