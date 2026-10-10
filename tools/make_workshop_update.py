"""Build a one-off Workshop update task file for TaleWorlds.MountAndBlade.SteamWorkshop.exe.

Takes tools/WorkshopUpdate.xml (item ID, ModuleFolder, Tags) and adds:
  - ItemDescription = tools/STEAM-DESCRIPTION.bbcode, XML-escaped, line breaks as &#10;
  - ChangeNotes     = --notes (replaces the template's placeholder)
  - Visibility      = --visibility (Private / FriendsOnly / Public; omitted = leave as is)

Writes to %TEMP%\\smart_steward_workshop_update.xml by default (or --out) and prints the path.
The template's comments are dropped: the uploader wants <Tasks> as the very first node.
(Same tool as ..\\better-skirmisher-separation's.)

    python tools\\make_workshop_update.py --notes "v1.0.0 - first release." --visibility Private
    & "<game>\\bin\\Win64_Shipping_Client\\TaleWorlds.MountAndBlade.SteamWorkshop.exe" <printed path>
"""
import argparse
import os
import re
import sys
import tempfile

TOOLS = os.path.dirname(os.path.abspath(__file__))
MAX_DESCRIPTION_BYTES = 8000          # Steam's cap, in UTF-8 bytes (ImmersiveAI's lesson: bytes, not chars)
VISIBILITIES = ("Private", "FriendsOnly", "Public")


def escape_attr(text: str) -> str:
    text = text.replace("\r\n", "\n").replace("\r", "\n")
    text = (text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
                .replace('"', "&quot;"))
    return text.replace("\t", "&#9;").replace("\n", "&#10;")


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--notes", required=True, help="ChangeNotes text (shown in the item's change log)")
    ap.add_argument("--visibility", choices=VISIBILITIES, help="omit to keep the current visibility")
    ap.add_argument("--no-description", action="store_true", help="do not send ItemDescription")
    ap.add_argument("--template", default=os.path.join(TOOLS, "WorkshopUpdate.xml"))
    ap.add_argument("--description", default=os.path.join(TOOLS, "STEAM-DESCRIPTION.bbcode"))
    ap.add_argument("--out", default=os.path.join(tempfile.gettempdir(), "smart_steward_workshop_update.xml"))
    args = ap.parse_args()

    with open(args.template, encoding="utf-8") as f:
        xml = f.read()
    xml = re.sub(r"\s*<!--.*?-->", "", xml, flags=re.S)  # comments out; <Tasks> stays first

    if not re.search(r'<ItemId Value="\d+"', xml):
        sys.exit("template has no numeric ItemId - fill in WorkshopUpdate.xml after the create")

    xml, n = re.subn(r'<ChangeNotes Value="[^"]*"\s*/>',
                     lambda m: f'<ChangeNotes Value="{escape_attr(args.notes)}"/>', xml)
    if n != 1:
        sys.exit("template needs exactly one <ChangeNotes .../>")

    extra = []
    if not args.no_description:
        with open(args.description, encoding="utf-8-sig") as f:
            desc = f.read().strip().replace("\r\n", "\n")
        size = len(desc.encode("utf-8"))
        if size > MAX_DESCRIPTION_BYTES:
            sys.exit(f"description is {size} UTF-8 bytes, Steam's limit is {MAX_DESCRIPTION_BYTES}")
        if re.search(r"STEAM_WORKSHOP_URL|TODO|FILL_IN", desc):
            sys.exit("description still contains a placeholder")
        extra.append(f'<ItemDescription Value="{escape_attr(desc)}"/>')
    if args.visibility:
        extra.append(f'<Visibility Value="{args.visibility}"/>')
    if extra:
        insert = "".join(f"        {e}\n" for e in extra)
        if "    </UpdateItem>" not in xml:
            sys.exit("template has no '    </UpdateItem>' line")
        xml = xml.replace("    </UpdateItem>", insert + "    </UpdateItem>", 1)

    xml = xml.lstrip()
    if not xml.startswith("<Tasks>"):
        sys.exit("output does not start with <Tasks>")
    with open(args.out, "w", encoding="utf-8", newline="\n") as f:
        f.write(xml)
    print(args.out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
