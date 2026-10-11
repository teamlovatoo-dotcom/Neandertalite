# Level check (outside Unity)

`CatacombsLayout.cs` (the "Підземелля" map) is pure maths, so it can be generated, checked and rendered here.

```
dotnet run -c Release -- /tmp/lc            # writes layout.json + height.bin
python3 -I check.py /tmp/lc                 # automatic checks
blender -b -P render_blender.py -- /tmp/lc /tmp/lc/r overview waterfall west under
```

`check.py` reports:
- **pokes_out** – underground boxes sticking out of the ground where the terrain isn't cut away;
- **hole_without_floor** / **open_hole_edge** – terrain cut-outs with nothing under them or with a visible edge;
- **water_zfight** / **water_edge_hanging** / **hole_under_water** – water flickering with the banks or hanging in the air;
- **\*_height** / **\*_in_wall** – spawns, pickups, decoration and torches floating, buried or inside walls.
