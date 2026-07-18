using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CursorCycle.Infrastructure;

internal static class JsonFile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    public static T? Load<T>(string path)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, Options);
    }

    public static void Save<T>(string path, T value)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("保存先フォルダーを取得できません。");

        Directory.CreateDirectory(directory);

        var temporaryPath = path + ".tmp";
        var json = JsonSerializer.Serialize(value, Options);
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, path, true);
    }

    public static void PreserveBrokenFile(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var backupPath = $"{path}.broken-{timestamp}";

        try
        {
            File.Copy(path, backupPath, false);
        }
        catch
        {
            // 壊れた元ファイルは残っているため、バックアップ作成失敗は読み込み側へ波及させない。
        }
    }
}
