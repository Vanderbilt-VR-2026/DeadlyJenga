"""Download Metro Nashville open data and orthophotos for campus + ring.

What it gets (all clipped to the box around config/boundary.geojson):
  footprints   Metro "Building Footprints" (Nashville Open Data, ArcGIS feature
               service; has Height and RoofType fields). GeoJSON pages, WGS84.
  canopy       Metro "Urban Tree Canopy 2021" polygons. GeoJSON pages, WGS84.
  ortho        Metro orthoimagery map service (maps.nashville.gov, newest year,
               default 2026) exported at --res m/px in UTM 16N (EPSG:32616)
               as PNG chunks, each with a world file (.pgw) and .prj.
  naip         Fallback: USGS-hosted NAIP image service (public domain, ~0.6 m),
               exported the same way.
Beside each dataset it saves the service/item description as JSON and the
metadata PDF when Metro publishes one, so the terms we saw are kept.

Output: raw/metro/<YYYY-MM-DD>/<dataset>/...  (write-once, in the manifest, stage 0)
Running again on the same day skips files already present with a matching hash.
"""
import argparse
import json
import math
import sys
from pathlib import Path

from pyproj import CRS, Transformer
from shapely.geometry import shape
from shapely.ops import unary_union

sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import CONFIG, data_root, fail  # noqa: E402
import manifest  # noqa: E402
import netfetch  # noqa: E402

AGOL_ITEM = "https://www.arcgis.com/sharing/rest/content/items/{id}"
METRO_TERMS = ("Metro Nashville Open Data: no explicit license; published under Metro Open Data "
               "Executive Order FO-018 (https://www.nashville.gov/departments/metro-clerk/legal-resources/"
               "executive-orders/mayor-freddie-oconnell/fo018). Credit: Metropolitan Government of "
               "Nashville and Davidson County. Contact opendata@nashville.gov.")
ORTHO_TERMS = ("Metro Nashville public map service (maps.nashville.gov); no license or use terms stated in "
               "the service; earlier years credit 'Metro GIS; Pictometry International'. Reference use "
               "only until terms are confirmed.")
NAIP_TERMS = "Public domain (USDA Farm Production and Conservation, NAIP; served by USGS The National Map)"

FEATURE_SETS = {
    "footprints": {"item": "193c44177c814ac7ab3a605c5a407b05", "pdf": "ac6c3a56fca747cfac8aac74e40e61dd",
                   "layer": "https://services2.arcgis.com/HdTo6HJqh92wn4D8/arcgis/rest/services/"
                            "Building_Footprints_view/FeatureServer/0"},
    "canopy": {"item": "702e9efe5b4f4ccf90c267c5c3a4ba87", "pdf": "015392a9c4084fc3a0f097e6ec3709e7",
               "layer": "https://services2.arcgis.com/HdTo6HJqh92wn4D8/arcgis/rest/services/"
                        "Urban_Tree_Canopy_2021_view/FeatureServer/0"},
}
ORTHO_SERVICE = "https://maps.nashville.gov/arcgis/rest/services/Imagery/{year}Imagery_WGS84/MapServer"
NAIP_SERVICE = "https://imagery.nationalmap.gov/arcgis/rest/services/USGSNAIPImagery/ImageServer"
UTM = 32616


def boundary_box(path, extra_m=0.0):
    """Lon/lat box and UTM box around campus + ring."""
    if not path.exists():
        fail(f"{path} missing; run make_boundary.py first")
    fc = json.loads(path.read_text(encoding="utf-8"))
    geom = unary_union([shape(f["geometry"]) for f in fc["features"]])
    to_utm = Transformer.from_crs(4326, UTM, always_xy=True)
    xs, ys = zip(*[to_utm.transform(x, y) for x, y in geom.exterior.coords]) if geom.geom_type == "Polygon" \
        else zip(*[to_utm.transform(x, y) for g in geom.geoms for x, y in g.exterior.coords])
    utm = (min(xs) - extra_m, min(ys) - extra_m, max(xs) + extra_m, max(ys) + extra_m)
    return geom.bounds, utm


def save_json_once(obj_bytes, dest, source, lic, db, root):
    digest, new = netfetch.save_bytes_once(obj_bytes, dest, source, lic, db, root)
    print(("saved " if new else "have ") + manifest.rel(dest, root))


def fetch_features(name, cfg, lonlat_box, folder, db, root):
    """Page through a feature layer, saving each GeoJSON page as it arrived."""
    out = folder / name
    item = netfetch.get(AGOL_ITEM.format(id=cfg["item"]), params={"f": "json"}).content
    save_json_once(item, out / "item.json", AGOL_ITEM.format(id=cfg["item"]), METRO_TERMS, db, root)
    layer = netfetch.get(cfg["layer"], params={"f": "json"}).content
    save_json_once(layer, out / "layer.json", cfg["layer"], METRO_TERMS, db, root)
    if cfg.get("pdf"):
        meta = json.loads(netfetch.get(AGOL_ITEM.format(id=cfg["pdf"]), params={"f": "json"}).content)
        netfetch.download_once(AGOL_ITEM.format(id=cfg["pdf"]) + "/data", out / (meta.get("name") or "metadata.pdf"),
                               METRO_TERMS, db, root)
    w, s, e, n = lonlat_box
    geom = json.dumps({"xmin": w, "ymin": s, "xmax": e, "ymax": n, "spatialReference": {"wkid": 4326}})
    base = {"where": "1=1", "geometry": geom, "geometryType": "esriGeometryEnvelope", "inSR": 4326,
            "spatialRel": "esriSpatialRelIntersects", "outFields": "*", "outSR": 4326, "f": "geojson"}
    count = netfetch.get(cfg["layer"] + "/query", params={**base, "returnCountOnly": "true", "f": "json"}).json()
    total = count.get("count")
    if total is None:
        fail(f"{name}: count query failed: {count}")
    page_size = json.loads(layer).get("maxRecordCount", 1000) or 1000
    print(f"{name}: {total} features in the box, pages of {page_size}")
    offset, page, got = 0, 0, 0
    while offset < total:
        dest = out / f"{name}_page{page:03d}.geojson"
        if netfetch.already_have(dest, db, root):
            n = len(json.loads(dest.read_text(encoding="utf-8"))["features"])
        else:
            r = netfetch.get(cfg["layer"] + "/query", params={**base, "resultOffset": offset,
                                                              "resultRecordCount": page_size, "orderByFields": "OBJECTID"})
            data = r.json()
            if not data.get("features"):
                fail(f"{name}: empty or bad page at offset {offset}: {r.text[:300]}")
            netfetch.save_bytes_once(r.content, dest, r.url, METRO_TERMS, db, root)
            n = len(data["features"])
        got += n
        offset += n          # the server may send fewer than asked; continue from what arrived
        page += 1
    print(f"{name}: saved {got} features in {page} page(s)")
    if got != total:
        fail(f"{name}: expected {total} features, saved {got}")


def not_png(path):
    """Error text when an export came back as something other than a PNG (e.g. an error page)."""
    head = Path(path).read_bytes()[:200]
    png_signature = bytes([0x89]) + b"PNG"
    return None if head[:4] == png_signature else f"not a PNG: {head!r}"


def world_file(x0, y1, res):
    """World file text: pixel size, rotation (0), and the center of the top-left pixel."""
    return f"{res:.6f}\n0.0\n0.0\n{-res:.6f}\n{x0 + res / 2:.6f}\n{y1 - res / 2:.6f}\n"


def export_raster(name, service, kind, utm_box, res, chunk_px, folder, lic, db, root, extra=None):
    """Export the box in chunks; save PNG + .pgw + .prj for each."""
    out = folder / name
    info = netfetch.get(service, params={"f": "json"}).content
    save_json_once(info, out / "service.json", service, lic, db, root)
    x0, y0, x1, y1 = utm_box
    step = chunk_px * res
    cols, rows = math.ceil((x1 - x0) / step), math.ceil((y1 - y0) / step)
    prj = CRS.from_epsg(UTM).to_wkt(version="WKT1_ESRI").encode()
    print(f"{name}: {cols} x {rows} chunks of {chunk_px}px at {res} m/px")
    for r in range(rows):
        for c in range(cols):
            cx0, cy1 = x0 + c * step, y1 - r * step
            bbox = f"{cx0},{cy1 - step},{cx0 + step},{cy1}"
            stem = f"{name}_r{r:02d}_c{c:02d}"
            params = {"bbox": bbox, "bboxSR": UTM, "imageSR": UTM, "size": f"{chunk_px},{chunk_px}",
                      "format": "png24" if kind == "map" else "png", "f": "image"}
            if kind == "map":
                params["transparent"] = "false"
            params.update(extra or {})
            url = service + ("/export" if kind == "map" else "/exportImage")
            dest = out / (stem + ".png")
            digest, new = netfetch.download_once(url, dest, lic, db, root, params=params, check=not_png)
            netfetch.save_bytes_once(world_file(cx0, cy1, res).encode(), out / (stem + ".pgw"),
                                     "world file written by fetch_metro.py", lic, db, root)
            netfetch.save_bytes_once(prj, out / (stem + ".prj"), "EPSG:32616 WKT written by fetch_metro.py",
                                     lic, db, root)
            print(f"  {'got' if new else 'have'} {stem}.png")


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--only", nargs="*", choices=["footprints", "canopy", "ortho", "naip"],
                   help="datasets to fetch (default: all)")
    p.add_argument("--date", default=netfetch.today(), help="snapshot folder name (default: today)")
    p.add_argument("--boundary", default=str(CONFIG / "boundary.geojson"))
    p.add_argument("--ortho-year", default="2026", help="Metro imagery year (default 2026, the newest)")
    p.add_argument("--res", type=float, default=0.15, help="orthophoto meters per pixel (default 0.15)")
    p.add_argument("--naip-res", type=float, default=0.6, help="NAIP meters per pixel (default 0.6)")
    p.add_argument("--chunk", type=int, default=2000,
                   help="pixels per exported chunk side (default 2000; bigger chunks time out on the Metro server)")
    args = p.parse_args(argv)
    todo = args.only or ["footprints", "canopy", "ortho", "naip"]

    root = data_root()
    folder = root / "raw" / "metro" / args.date
    db = manifest.connect(root)
    lonlat_box, utm_box = boundary_box(Path(args.boundary), extra_m=20.0)
    # Snap the UTM box to whole meters so chunk edges are tidy.
    utm_box = (math.floor(utm_box[0]), math.floor(utm_box[1]), math.ceil(utm_box[2]), math.ceil(utm_box[3]))
    print(f"box lon/lat {tuple(round(v, 6) for v in lonlat_box)}; UTM16N {utm_box}")

    for name in ("footprints", "canopy"):
        if name in todo:
            fetch_features(name, FEATURE_SETS[name], lonlat_box, folder, db, root)
    if "ortho" in todo:
        export_raster(f"ortho_{args.ortho_year}", ORTHO_SERVICE.format(year=args.ortho_year), "map",
                      utm_box, args.res, args.chunk, folder, ORTHO_TERMS, db, root)
    if "naip" in todo:
        export_raster("naip", NAIP_SERVICE, "image", utm_box, args.naip_res, args.chunk, folder,
                      NAIP_TERMS, db, root, extra={"interpolation": "RSP_BilinearInterpolation"})
    db.close()


if __name__ == "__main__":
    main()
