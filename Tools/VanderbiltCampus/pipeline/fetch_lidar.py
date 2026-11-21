"""Download USGS 3DEP LiDAR tiles (LAZ) that cover the campus and backdrop ring.

Projects (pick with --project, default TN_DavidsonCounty_D22):
  TN_DavidsonCounty_D22  Davidson County 2022, Quality Level 1. The main source.
  TN_NASHVILLE_2011      Nashville 2011 (about 1 point per m2). Comparison only.

How tiles are found: The National Map (TNM) products API lists every LiDAR
tile whose box touches our area. We keep the tiles of the chosen project whose
box touches campus + ring (config/boundary.geojson). Tiles come from the USGS
rockyweb staging folder; per-tile and project metadata come from the USGS
prd-tnm bucket (the same files the staging folder links to).

Output:
  raw/lidar/<project>/LAZ/*.laz                tiles, untouched
  raw/lidar/<project>/metadata/...             project report, metadata XML, tile index
  derived/lidar_index/<project>/tile_index.json  our own record: tile name, URL,
        lon/lat box, sha256, plus the CRS, point density and class counts read
        from the files with laspy.
Everything is recorded in the manifest (stage 0, USGS public domain).
Running again skips files already downloaded with a matching hash.
"""
import argparse
import json
import shlex
import subprocess
import sys
import time
from collections import Counter
from pathlib import Path

import laspy
import numpy as np
from shapely.geometry import box, shape
from shapely.ops import unary_union

sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import CONFIG, data_root, fail, local_config, save_json  # noqa: E402
import manifest  # noqa: E402
import netfetch  # noqa: E402

TNM_API = "https://tnmaccess.nationalmap.gov/api/v1/products"
LICENSE = "Public domain (USGS 3DEP); credit: U.S. Geological Survey"
S3 = "https://prd-tnm.s3.amazonaws.com/StagedProducts/Elevation"
# USGS 3DEP on AWS Open Data: the same projects as Entwine Point Tiles (EPT),
# reprojected to Web Mercator (EPSG:3857) horizontally; Z keeps the project's units.
EPT = "https://s3-us-west-2.amazonaws.com/usgs-lidar-public/{name}/ept.json"

PROJECTS = {
    "TN_DavidsonCounty_D22": {
        "tnm_name": "TN_DavidsonCounty_D22",
        "ept": "TN_DavidsonCo_1_2022",
        "tile_xml": S3 + "/LPC/Projects/TN_DavidsonCounty_D22/TN_DavidsonCo_1_2022/metadata/{stem}.xml",
        "extra": {   # local path under metadata/ : URL
            "0_file_download_links.txt": S3 + "/LPC/Projects/TN_DavidsonCounty_D22/TN_DavidsonCo_1_2022/0_file_download_links.txt",
            "TN_DavidsonCo_1_2022.vpc": S3 + "/LPC/Projects/TN_DavidsonCounty_D22/TN_DavidsonCo_1_2022/TN_DavidsonCo_1_2022.vpc",
            "reports/TN_Davidson_County_Lidar2022_D22_Report_WU300056.pdf": S3 + "/metadata/TN_DavidsonCounty_D22/TN_DavidsonCo_1_2022/reports/TN_Davidson_County_Lidar2022_D22_Report_WU300056.pdf",
            "reports/USGS_TN_DavidsonCo_1_2022_Summary_Report.pdf": S3 + "/metadata/TN_DavidsonCounty_D22/TN_DavidsonCo_1_2022/reports/USGS_TN_DavidsonCo_1_2022_Summary_Report.pdf",
            "reports/USGS_TN_DavidsonCo_1_2022_FINAL_LPC_Report.txt": S3 + "/metadata/TN_DavidsonCounty_D22/TN_DavidsonCo_1_2022/reports/USGS_TN_DavidsonCo_1_2022_FINAL_LPC_Report.txt",
            "reports/TN_Davidson_County_Lidar2022_D22_Report_WU300056_Classified_Point_Cloud_Metadata.xml": S3 + "/metadata/TN_DavidsonCounty_D22/TN_DavidsonCo_1_2022/reports/vendor_provided_xml/TN_Davidson_County_Lidar2022_D22_Report_WU300056_Classified_Point_Cloud_Metadata.xml",
            "Vertical_Accuracy/USGS_TN_DavidsonCo_1_2022_VA.txt": S3 + "/metadata/TN_DavidsonCounty_D22/TN_DavidsonCo_1_2022/Vertical_Accuracy/USGS/USGS_TN_DavidsonCo_1_2022_VA.txt",
            "spatial_metadata/USGS_TN_DavidsonCo_1_2022_TileIndex.zip": S3 + "/metadata/TN_DavidsonCounty_D22/TN_DavidsonCo_1_2022/spatial_metadata/USGS/USGS_TN_DavidsonCo_1_2022_TileIndex.zip",
        },
    },
    "TN_NASHVILLE_2011": {
        "tnm_name": "TN_NASHVILLE_2011",
        "ept": "TN_Nashville_2011",
        "tile_xml": S3 + "/LPC/Projects/legacy/TN_NASHVILLE_2011/metadata/{stem}.xml",
        "extra": {
            "Nashville_TN_1.0_Meter_LiDAR_Project.xml": S3 + "/metadata/legacy/TN_NASHVILLE_2011/Nashville_TN_1.0_Meter_LiDAR_Project.xml",
            "Nashville_TN_1.0_Meter_LiDAR_Classified_Point_Cloud.xml": S3 + "/metadata/legacy/TN_NASHVILLE_2011/Nashville_TN_1.0_Meter_LiDAR_Classified_Point_Cloud.xml",
            "Nashville_TN_1.0_Meter_LiDAR_Project_Report.pdf": S3 + "/metadata/legacy/TN_NASHVILLE_2011/Nashville_TN_1.0_Meter_LiDAR_Project_Report.pdf",
            **{f"Nashville_1.0_Meter_LiDAR_Tile_Index_UTM.{ext}":
               S3 + f"/metadata/legacy/TN_NASHVILLE_2011/Nashville_1.0_Meter_LiDAR_Tile_Index_UTM.{ext}"
               for ext in ("shp", "shx", "dbf", "prj")},
        },
    },
}


def area_of_interest(path):
    """Campus + ring from boundary.geojson, as one lon/lat shape."""
    if not path.exists():
        fail(f"{path} missing; run make_boundary.py first")
    fc = json.loads(path.read_text(encoding="utf-8"))
    return unary_union([shape(f["geometry"]) for f in fc["features"]])


def list_tiles(project, aoi):
    """Ask TNM for LiDAR tiles touching the area; keep the ones from this project."""
    w, s, e, n = aoi.bounds
    tiles, offset = {}, 0
    while True:
        r = netfetch.get(TNM_API, params={"datasets": "Lidar Point Cloud (LPC)",
                                          "bbox": f"{w},{s},{e},{n}", "max": 100,
                                          "offset": offset, "outputFormat": "JSON"}, timeout=120)
        page = r.json()
        items = page.get("items", [])
        for it in items:
            if f" {project['tnm_name']} " not in it["title"] + " ":
                continue
            bb = it["boundingBox"]
            footprint = box(bb["minX"], bb["minY"], bb["maxX"], bb["maxY"])
            if footprint.intersects(aoi):
                url = it["downloadLazURL"] or it["downloadURL"]
                tiles[Path(url).name] = {"name": Path(url).stem, "url": url,
                                         "bounds_lonlat": [bb["minX"], bb["minY"], bb["maxX"], bb["maxY"]],
                                         "size_listed": it.get("sizeInBytes"),
                                         "publication_date": it.get("publicationDate"),
                                         "metadata_page": it.get("metaUrl")}
        offset += len(items)
        if not items or offset >= page.get("total", 0):
            break
    return dict(sorted(tiles.items()))


def summarize_laz(path, count_classes):
    """Header facts (CRS, bounds, points) and optionally points per class."""
    with laspy.open(path) as f:
        h = f.header
        crs = h.parse_crs()
        info = {"points": h.point_count, "las_version": str(h.version), "point_format": h.point_format.id,
                "mins": [round(v, 3) for v in h.mins], "maxs": [round(v, 3) for v in h.maxs],
                "crs_name": crs.name if crs else None,
                "horizontal_epsg": None, "vertical_epsg": None, "crs_wkt": crs.to_wkt() if crs else None}
        if crs is not None:
            subs = crs.sub_crs_list or [crs]
            info["horizontal_epsg"] = subs[0].to_epsg()
            info["horizontal_units"] = subs[0].axis_info[0].unit_name if subs[0].axis_info else None
            if len(subs) > 1:
                info["vertical_epsg"] = subs[1].to_epsg()
                info["vertical_name"] = subs[1].name
                info["vertical_units"] = subs[1].axis_info[0].unit_name if subs[1].axis_info else None
        if count_classes:
            classes, first_returns = Counter(), 0
            for chunk in f.chunk_iterator(2_000_000):
                c = np.asarray(chunk.classification)
                vals, counts = np.unique(c, return_counts=True)
                classes.update({int(v): int(n) for v, n in zip(vals, counts)})
                first_returns += int(np.count_nonzero(np.asarray(chunk.return_number) == 1))
            info["class_counts"] = dict(sorted(classes.items()))
            info["first_returns"] = first_returns
    return info


def fetch_ept(project, proj, aoi, folder, db, root):
    """Fallback when the staging server is too slow: crop the USGS EPT copy with PDAL.

    Writes raw/lidar/<project>/EPT/<project>_campus_ring_ept.laz (EPSG:3857, Z in
    the project's original vertical units) plus the ept.json it came from.
    """
    from pyproj import Transformer
    url = EPT.format(name=proj["ept"])
    netfetch.download_once(url, folder / "EPT" / "ept.json", LICENSE, db, root)
    to_merc = Transformer.from_crs(4326, 3857, always_xy=True)
    w, s, e, n = aoi.bounds
    xs, ys = zip(*[to_merc.transform(x, y) for x, y in ((w, s), (e, s), (e, n), (w, n))])
    bounds = f"([{min(xs):.2f}, {max(xs):.2f}], [{min(ys):.2f}, {max(ys):.2f}])"
    dest = folder / "EPT" / f"{project}_campus_ring_ept.laz"
    if netfetch.already_have(dest, db, root):
        print(f"have {dest.name}")
        return dest
    part = dest.with_name(dest.stem + ".part.laz")
    pipeline = {"pipeline": [{"type": "readers.ept", "filename": url, "bounds": bounds},
                             {"type": "writers.las", "filename": str(part), "compression": "laszip",
                              "minor_version": 4, "dataformat_id": 6, "forward": "all", "extra_dims": "all"}]}
    pfile = folder / "EPT" / "pdal_pipeline.json"
    pfile.parent.mkdir(parents=True, exist_ok=True)
    pfile.write_text(json.dumps(pipeline, indent=1), encoding="utf-8")
    pdal = local_config().get("pdal") or "pdal"
    print(f"PDAL: reading {url} inside {bounds} (EPSG:3857)")
    result = subprocess.run(shlex.split(pdal, posix=False) + ["pipeline", str(pfile)])
    pfile.unlink()
    if result.returncode != 0 or not part.exists():
        fail("PDAL could not read the EPT copy")
    part.replace(dest)
    manifest.add_file(dest, source=f"{url} cropped with PDAL readers.ept bounds={bounds} EPSG:3857",
                      license=LICENSE, stage=0, date=netfetch.today(), root=root, db=db)
    db.commit()
    return dest


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--project", default="TN_DavidsonCounty_D22", choices=sorted(PROJECTS))
    p.add_argument("--boundary", default=str(CONFIG / "boundary.geojson"))
    p.add_argument("--list-only", action="store_true", help="print the tiles and stop")
    p.add_argument("--no-classes", action="store_true", help="skip counting classes in the first tile")
    p.add_argument("--pause", type=float, default=2.0, help="seconds between downloads (be polite)")
    p.add_argument("--source", choices=["tiles", "ept"], default="tiles",
                   help="tiles = original LAZ tiles from the USGS staging server (default); "
                        "ept = crop of the USGS EPT copy on AWS (fallback, EPSG:3857)")
    args = p.parse_args(argv)

    proj = PROJECTS[args.project]
    root = data_root()
    folder = root / "raw" / "lidar" / args.project
    aoi = area_of_interest(Path(args.boundary))
    tiles = list_tiles(proj, aoi)
    if not tiles:
        fail(f"TNM lists no {args.project} tiles over the boundary")
    total = sum(t["size_listed"] or 0 for t in tiles.values())
    print(f"{args.project}: {len(tiles)} tiles touch campus + ring, {total / 1e6:.0f} MB listed")
    for name, t in tiles.items():
        print(f"  {name}  {t['size_listed'] / 1e6:.0f} MB")
    if args.list_only:
        return

    db = manifest.connect(root)
    # Project metadata and reports.
    for local, url in proj["extra"].items():
        digest, new = netfetch.download_once(url, folder / "metadata" / local, LICENSE, db, root)
        print(("got " if new else "have ") + local)
    if args.source == "ept":
        dest = fetch_ept(args.project, proj, aoi, folder, db, root)
        info = summarize_laz(dest, count_classes=not args.no_classes)
        area_m2 = (info["maxs"][0] - info["mins"][0]) * (info["maxs"][1] - info["mins"][1])
        # Web Mercator stretches distances by 1/cos(latitude); undo it for a true area.
        area_m2 *= np.cos(np.radians(aoi.centroid.y)) ** 2
        doc = {"project": args.project, "source": "USGS 3DEP EPT on AWS (cropped)", "license": LICENSE,
               "fetched": netfetch.today(), "file": manifest.rel(dest, root), "sha256": manifest.sha256(dest),
               "staging_tiles_touching_area": list(tiles.values()), "header": info,
               "density_pts_per_m2": {"all_points": round(info["points"] / area_m2, 2),
                                      "first_returns": round(info.get("first_returns", 0) / area_m2, 2),
                                      "note": "over the crop's box"},
               "note": "Horizontal EPSG:3857; Z is in the project's original vertical units (see project metadata)."}
        out = root / "derived" / "lidar_index" / args.project / "ept_index.json"
        save_json(out, doc)
        manifest.add_file(out, source="fetch_lidar.py --source ept", license=LICENSE, stage=0, root=root, db=db)
        db.commit()
        db.close()
        print(f"points {info['points']}, classes {info.get('class_counts')}, density {doc['density_pts_per_m2']}")
        print(f"wrote {out}")
        return
    # Tiles and their per-tile metadata XML.
    for i, (name, t) in enumerate(tiles.items(), 1):
        dest = folder / "LAZ" / name
        digest, new = netfetch.download_once(t["url"], dest, LICENSE, db, root)
        t["sha256"], t["size"] = digest, dest.stat().st_size
        print(f"[{i}/{len(tiles)}] {'got' if new else 'have'} {name} ({t['size'] / 1e6:.0f} MB)")
        if new:
            time.sleep(args.pause)
        xml_url = proj["tile_xml"].format(stem=t["name"])
        xml_dest = folder / "metadata" / "tiles" / (t["name"] + ".xml")
        try:
            netfetch.download_once(xml_url, xml_dest, LICENSE, db, root)
            t["metadata_xml"] = manifest.rel(xml_dest, root)
        except SystemExit:
            print(f"  (no per-tile XML at {xml_url}; continuing)")
            t["metadata_xml"] = None

    # Read the files: CRS from every header; classes and density from the tile
    # that holds the middle of campus (a full tile, not a half-empty edge one).
    mid = aoi.centroid
    center = next((n for n, t in tiles.items() if box(*t["bounds_lonlat"]).contains(mid)), next(iter(tiles)))
    for name, t in tiles.items():
        info = summarize_laz(folder / "LAZ" / name, count_classes=(name == center and not args.no_classes))
        t["header"] = {k: v for k, v in info.items() if k != "crs_wkt"}
        if name == center:
            first = info
    epsgs = Counter((t["header"]["horizontal_epsg"], t["header"]["vertical_epsg"]) for t in tiles.values())
    w = first["maxs"][0] - first["mins"][0]
    h = first["maxs"][1] - first["mins"][1]
    unit = first.get("horizontal_units") or ""
    to_m = 0.3048006096 if "US survey foot" in unit else (0.3048 if "foot" in unit.lower() else 1.0)
    area_m2 = w * h * to_m * to_m
    density = {"tile": center, "all_points": first.get("points") and round(first["points"] / area_m2, 2),
               "first_returns": first.get("first_returns") and round(first["first_returns"] / area_m2, 2),
               "note": "points per m2 over the tile's header box (edge tiles may be partly empty)"}
    index_doc = {"project": args.project, "license": LICENSE, "fetched": netfetch.today(),
                 "crs_combos_seen": [list(k) for k in epsgs], "crs_wkt": first["crs_wkt"],
                 "horizontal_units": first.get("horizontal_units"), "vertical_units": first.get("vertical_units"),
                 "vertical_name": first.get("vertical_name"), "density_pts_per_m2": density,
                 "class_counts": {"tile": center, "counts": first.get("class_counts")},
                 "tiles": list(tiles.values())}
    out = root / "derived" / "lidar_index" / args.project / "tile_index.json"
    save_json(out, index_doc)
    manifest.add_file(out, source="fetch_lidar.py (TNM API + laspy headers)", license=LICENSE,
                      stage=0, root=root, db=db)
    db.commit()
    db.close()
    print(f"CRS (horizontal, vertical EPSG): {dict(epsgs)}; units {first.get('horizontal_units')} / "
          f"{first.get('vertical_units')}")
    print(f"density: {density}")
    print(f"classes in {center}: {first.get('class_counts')}")
    print(f"wrote {out}")


if __name__ == "__main__":
    main()
