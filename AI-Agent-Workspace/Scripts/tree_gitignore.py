import os
import argparse
from pathlib import Path
import pathspec

def find_nearest_gitignore(start_path: Path) -> Path | None:
    """Find the nearest .gitignore file in start_path or its parents (first match walking up)."""
    current_path = Path(start_path).resolve()
    while True:
        candidate = current_path / '.gitignore'
        if candidate.is_file():
            return candidate
        parent = current_path.parent
        if parent == current_path:  # Reached filesystem root
            return None
        current_path = parent

def find_all_local_gitignores(root: Path) -> list[Path]:
    """Recursively find all .gitignore files under the given root directory (inclusive)."""
    root = root.resolve()
    found: list[Path] = []
    # Include the nearest .gitignore from parents as the root-most policy (if present)
    nearest = find_nearest_gitignore(root)
    if nearest:
        found.append(nearest)
    # Walk down from root and collect any nested .gitignore files
    for dirpath, dirnames, filenames in os.walk(root):
        # Skip the .git folder entirely to avoid noise
        if '.git' in dirnames:
            dirnames.remove('.git')
        if '.venv' in dirnames:
            dirnames.remove('.venv')
        if '.pytest_cache' in dirnames:
            dirnames.remove('.pytest_cache')
        if '.mypy_cache' in dirnames:
            dirnames.remove('.mypy_cache')
        if '.VSCodeCounter' in dirnames:
            dirnames.remove('.VSCodeCounter')
        if '.vscode' in dirnames:
            # keep .vscode but don't descend into if desired; here we keep it
            pass
        if '.gitignore' in filenames:
            found.append(Path(dirpath) / '.gitignore')
    # De-duplicate while preserving order
    seen = set()
    unique: list[Path] = []
    for p in found:
        rp = p.resolve()
        if rp not in seen:
            unique.append(rp)
            seen.add(rp)
    return unique

def load_gitignore_patterns(gitignore_path: Path | None) -> list[str]:
    """Load patterns from a .gitignore file (single file)."""
    if not gitignore_path:
        return []
    with open(gitignore_path, 'r', encoding='utf-8') as f:
        return [line for line in f.read().splitlines() if line and not line.strip().startswith('#')]

def compile_gitignore_specs(gitignore_files: list[Path]) -> list[tuple[Path, pathspec.PathSpec]]:
    """Compile a list of (base_dir, PathSpec) for each .gitignore file provided.

    Each .gitignore's patterns apply relative to its directory, similar to Git semantics.
    We intentionally keep them separate so we can evaluate a file against all relevant specs.
    """
    compiled: list[tuple[Path, pathspec.PathSpec]] = []
    for gi in gitignore_files:
        base = gi.parent.resolve()
        patterns = load_gitignore_patterns(gi)
        # Common ignores that should apply regardless (these can also be in top-level .gitignore)
        # We DON'T add them here to avoid double-applying per directory. We'll apply global ignores separately.
        spec = pathspec.PathSpec.from_lines(pathspec.patterns.GitWildMatchPattern, patterns)
        compiled.append((base, spec))
    return compiled

def is_ignored(path_abs: Path, specs_by_dir: list[tuple[Path, pathspec.PathSpec]], global_spec: pathspec.PathSpec) -> bool:
    """Return True if the absolute path is matched by any local or global ignore spec."""
    # Always evaluate global spec first
    rel_for_global = path_abs.as_posix()
    if global_spec.match_file(rel_for_global):
        return True
    # Evaluate each local .gitignore spec relative to its base directory
    for base_dir, spec in specs_by_dir:
        try:
            rel = path_abs.relative_to(base_dir).as_posix()
        except ValueError:
            # path_abs not under base_dir
            continue
        if spec.match_file(rel):
            return True
    return False

def build_tree(directory: Path, specs_by_dir: list[tuple[Path, pathspec.PathSpec]], global_spec: pathspec.PathSpec, prefix: str = ''):
    """Recursively print the directory tree, honoring multiple local .gitignore files."""
    current_dir_path = Path(directory).resolve()
    try:
        # Get absolute paths first
        items_abs = sorted(list(current_dir_path.iterdir()), key=lambda x: (x.is_file(), x.name.lower()))
    except PermissionError:
        print(f"{prefix}└── [ACCESS DENIED] {current_dir_path.name}/")
        return
    except FileNotFoundError:
        print(f"Error: Directory not found: {current_dir_path}")
        return

    # Filter items based on combined .gitignore specs
    filtered_items_abs = [item_abs for item_abs in items_abs if not is_ignored(item_abs, specs_by_dir, global_spec)]

    pointers = ['├── ' for _ in range(len(filtered_items_abs) - 1)] + ['└── ']

    for pointer, item_abs_path in zip(pointers, filtered_items_abs):
        print(f"{prefix}{pointer}{item_abs_path.name}{'/' if item_abs_path.is_dir() else ''}")

        if item_abs_path.is_dir():
            extension = '│   ' if pointer == '├── ' else '    '
            build_tree(item_abs_path, specs_by_dir, global_spec, prefix=prefix + extension)

def main():
    parser = argparse.ArgumentParser(description='List directory contents like tree, respecting .gitignore.')
    parser.add_argument('directory', nargs='?', default='.', help='The directory to list (default: current directory)')
    args = parser.parse_args()

    start_dir = Path(args.directory).resolve()

    if not start_dir.is_dir():
        print(f"Error: '{start_dir}' is not a valid directory.")
        return

    # Gather all .gitignore files affecting this tree
    local_gitignores = find_all_local_gitignores(start_dir)
    specs_by_dir = compile_gitignore_specs(local_gitignores)

    # Global ignores (apply regardless of directory); expressed as absolute-like posix substrings
    global_patterns = [
        '**/.git/',
        '**/.venv/',
        '**/__pycache__/',
        '**/.pytest_cache/',
        '**/.mypy_cache/',
    ]
    global_spec = pathspec.PathSpec.from_lines(pathspec.patterns.GitWildMatchPattern, global_patterns)

    print(f"{start_dir.name}/ (multi .gitignore; bases={[str(p.parent) for p in local_gitignores]})")
    build_tree(start_dir, specs_by_dir, global_spec)

if __name__ == "__main__":
    main()