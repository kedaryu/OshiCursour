namespace CursorCycle.Domain;

public sealed record CursorRoleDefinition(
    string RegistryName,
    string DisplayName,
    IReadOnlyList<string> FileAliases);

public static class CursorRoles
{
    public static readonly IReadOnlyList<CursorRoleDefinition> All =
    [
        new("Arrow", "通常の選択", ["通常", "通常の選択", "arrow", "normal", "select"]),
        new("Help", "ヘルプの選択", ["ヘルプ", "ヘル", "help", "helpsel"]),
        new("AppStarting", "バックグラウンドで作業中", ["バックグラウンド", "作業中", "appstarting", "working", "background"]),
        new("Wait", "待ち状態", ["待機", "待ち状態", "wait", "busy"]),
        new("Crosshair", "領域選択", ["領域選択", "精密選択", "crosshair", "precision"]),
        new("IBeam", "テキスト選択", ["テキスト", "テキスト選択", "ibeam", "text"]),
        new("NWPen", "手書き", ["手書き", "nwpen", "pen", "handwriting"]),
        new("No", "利用不可", ["利用不可", "禁止", "no", "unavailable"]),
        new("SizeNS", "上下に拡大/縮小", ["上下", "上下拡大縮小", "sizens", "vertical"]),
        new("SizeWE", "左右に拡大/縮小", ["左右", "左右拡大縮小", "sizewe", "horizontal"]),
        new("SizeNWSE", "斜めに拡大/縮小 1", ["斜め1", "斜め１", "sizenwse", "diagonal1"]),
        new("SizeNESW", "斜めに拡大/縮小 2", ["斜め2", "斜め２", "sizenesw", "diagonal2"]),
        new("SizeAll", "移動", ["移動", "sizeall", "move"]),
        new("UpArrow", "代替選択", ["代替選択", "代替", "uparrow", "alternate"]),
        new("Hand", "リンクの選択", ["リンク", "リンクの選択", "hand", "link"]),
        new("Pin", "場所の選択", ["場所", "場所の選択", "ピン", "pin", "location"]),
        new("Person", "人の選択", ["人", "人物", "人の選択", "person"])
    ];

    public static CursorRoleDefinition Arrow => All[0];
}
