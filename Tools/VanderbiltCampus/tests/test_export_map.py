"""Fast checks of make_unity_map.py helpers (no archive needed)."""
import sys
from pathlib import Path

import numpy as np
from shapely.geometry import Point, Polygon, box

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "pipeline"))
import make_unity_map as mum  # noqa: E402


def test_grid_node_layout_south_origin():
    # heights[J, I] at X = 0 + I, Y = 0 + J (row 0 = south), value = 10 * Y + X
    J, I = np.mgrid[0:5, 0:5]
    g = mum.Grid(10.0 * J + I, {"origin_x": 0, "origin_y": 0, "cell_m": 1.0,
                                "layout": "heights[J, I] at node X = origin_x + I * cell_m ... (row 0 = south edge)"})
    assert np.allclose(g.sample([1.5, 4.0, 0.0], [2.25, 4.0, 0.0]), [24.0, 44.0, 0.0])


def test_grid_stage0_layout_north_origin():
    # Cell centers, origin = top-left corner, row 0 = north. Cell (r, c) center: X = c + .5, Y = 4 - r - .5
    r, c = np.mgrid[0:4, 0:4]
    g = mum.Grid(10.0 * (3.5 - r) + (c + 0.5), {"origin_x": 0, "origin_y": 4, "cell_m": 1.0, "row0": "north edge"})
    assert np.allclose(g.sample([1.0, 2.5], [2.0, 0.5]), [21.0, 7.5])


class FlatWorld:
    """Minimal stand-in for make_unity_map.World: flat open ground, one building."""
    def __init__(self, poly):
        self.union, self.campus = poly, box(-1000, -1000, 1000, 1000)
        self.built = {}

    def ground(self, x, y):
        return np.zeros(np.shape(np.atleast_1d(x)))

    def surface(self, x, y):
        return 20

    def tree_distance(self, x, y):
        return 1e9


def test_wall_anchor_faces_wall():
    poly = Polygon([(0, 0), (20, 0), (20, 10), (0, 10)])
    walls = mum.wall_anchors("B_1", poly, FlatWorld(poly))
    assert len(walls) == 4
    for L, x, y, nx, ny in walls:
        assert L >= 8
        # 0.6 m outside the wall; 1 m along forward (= -normal, as written to the json) is inside.
        assert not poly.contains(Point(x, y))
        assert abs(poly.exterior.distance(Point(x, y)) - 0.6) < 1e-6
        assert poly.contains(Point(x - nx, y - ny))


def test_to_unity_axes():
    assert mum.to_unity(10, 20, 5) == [10.0, 5.0, 20.0]


def test_building_across_path_removes_edge():
    """A straight path A-B with a built building across it: the route must take the detour A-C-D-B."""
    import networkx as nx
    pos = {"A": (0, 0), "B": (100, 0), "C": (0, 30), "D": (100, 30)}
    G = nx.Graph()
    for a, b in (("A", "B"), ("A", "C"), ("C", "D"), ("D", "B")):
        G.add_edge(a, b, weight=float(np.hypot(*np.subtract(pos[a], pos[b]))))
    building = box(45, -10, 55, 10)                          # collider outline across A-B
    blocked = building.buffer(mum.BODY_CLEAR)
    assert mum.drop_blocked_edges(G, pos, blocked) == 1
    assert nx.shortest_path(G, "A", "B", weight="weight") == ["A", "C", "D", "B"]
    # Passing 0.4 m from the wall is still inside the survivor clearance.
    near = {"E": (0, 10.4), "F": (100, 10.4)}
    H = nx.Graph()
    H.add_edge("E", "F", weight=100.0)
    assert mum.drop_blocked_edges(H, near, blocked) == 1


def test_grid_fallback_goes_around_building():
    building = box(45, -10, 55, 10)
    blocked = building.buffer(mum.BODY_CLEAR)

    class W:
        def ground(self, x, y):
            return np.zeros(np.shape(np.atleast_1d(x)))
    pts = mum.grid_route(W(), blocked, [], (0.0, 0.0), (100.0, 0.0), margin=30.0)
    assert pts is not None
    wps = mum.simplify_safe(pts, blocked)
    assert all(mum.clear_leg(blocked, p, q) for p, q in zip(wps, wps[1:]))
    assert max(abs(p[1]) for p in wps) >= 10.6 - 1.0          # it really went around
