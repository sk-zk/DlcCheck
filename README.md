# DlcCheck

DlcCheck is a command line tool which lists all DLC assets used by a ETS2 / ATS map, to check
which DLCs it requires. 

This is a sample project for [TruckLib](https://github.com/sk-zk/TruckLib).

Supports map version 884.

## Usage
`DlcCheck mbd_path game_root_path [output_path]`

**mbd_path**: Path to the .mbd file of your map.  
**game_root_path**: Path to your ETS2 / ATS install folder.  
**output_path** (optional): Path of the output file. Writes to console if not set.

### Example

`DlcCheck "[path to extracted europe.mbd]" "[path to ETS2 folder]" ./output.txt`

will write the following:

    The map "europe" uses assets from the following DLCs:

    * dlc_balkan_e.scs
    * dlc_balt.scs
    * dlc_east.scs
    * dlc_fr.scs
    * dlc_it.scs
    * dlc_north.scs

    Detailed list of all DLC assets:

    [ dlc_balkan_e.scs ]
    bld_scheme.blke_48
    bld_scheme.blke_49
    bld_scheme.blke_50
    (etc.)

## Dependencies

* [TruckLib](https://github.com/sk-zk/TruckLib)
