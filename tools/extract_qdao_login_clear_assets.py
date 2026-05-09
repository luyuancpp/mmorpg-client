import pathlib
import PIL

WORKSPACE: pathlib.Path = pathlib.Path(__file__).resolve().parents[3]
SOURCE: pathlib.Path = WORKSPACE / "image" / "q_daoist_login_clear_2560x1080.png"
OUT: pathlib.Path = pathlib.Path(__file__).resolve().parents[1] / "Assets" / "Resources" / "UI" / "qdao"

# Crops are from q_daoist_login_clear_2560x1080.png (2560x1080).
# They intentionally extract reusable UI pieces from the approved concept art.
CROPS: dict[str, tuple[int, int, int, int]] = {
    "qdao_login_banner.png": (1055, 20, 1710, 260),
    "qdao_server_scroll.png": (610, 112, 2030, 285),
    "qdao_cloud_corner.png": (1955, 850, 2248, 1010),
    "qdao_icon_talisman.png": (558, 860, 695, 1050),
    "qdao_icon_gate.png": (1302, 326, 1438, 464),
    "qdao_role_wanderer.png": (1810, 18, 2145, 350),
    "qdao_role_talisman.png": (100, 395, 300, 605),
    "qdao_role_sword.png": (1298, 550, 1438, 690),
    "qdao_server_row.png": (955, 315, 1422, 412),
    "qdao_server_row_alt.png": (1455, 315, 1926, 412),
    "qdao_side_tab.png": (646, 363, 886, 431),
    "qdao_search_box.png": (646, 306, 884, 361),
    "qdao_primary_button.png": (1722, 866, 1876, 931),
    "qdao_bottom_bar.png": (985, 848, 1915, 974),
}

KEEP_NAMES: set[str] = set(CROPS) | {"qdao_scene_backdrop.png"}


def clear_old_assets() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    for path: pathlib.Path in OUT.glob("*.png"):
        if path.name not in KEEP_NAMES:
            path.unlink()
            meta: pathlib.Path = path.with_suffix(path.suffix + ".meta")
            if meta.exists():
                meta.unlink()


def save_crop(src: PIL.Image.Image, name: str, box: tuple[int, int, int, int]) -> None:
    cropped = src.crop(box)
    cropped.save(OUT / name)


def main() -> None:
    if not SOURCE.exists():
        raise FileNotFoundError(SOURCE)
    OUT.mkdir(parents=True, exist_ok=True)
    clear_old_assets()

    src = PIL.Image.open(SOURCE).convert("RGBA")
    for name, box in CROPS.items():
        save_crop(src, name, box)


if __name__ == "__main__":
    main()
