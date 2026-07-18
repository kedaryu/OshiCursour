using System.Text;
using CursorCycle.Domain;

namespace CursorCycle.Infrastructure;

public sealed class CursorFolderScanner
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".ani", ".cur" };

    public CursorSetScanResult Scan(string folderPath)
    {
        var result = new CursorSetScanResult
        {
            FolderPath = folderPath
        };

        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            result.Warnings.Add("フォルダーが見つかりません。");
            return result;
        }

        string[] files;
        try
        {
            files = Directory.EnumerateFiles(folderPath, "*", SearchOption.TopDirectoryOnly)
                .Where(path => SupportedExtensions.Contains(Path.GetExtension(path)))
                .OrderByDescending(path =>
                    string.Equals(Path.GetExtension(path), ".ani", StringComparison.OrdinalIgnoreCase))
                .ThenBy(path => path, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
        catch (Exception exception)
        {
            result.Warnings.Add($"フォルダーを読み込めません: {exception.Message}");
            return result;
        }

        if (files.Length == 0)
        {
            result.Warnings.Add(".ani または .cur ファイルがありません。");
            return result;
        }

        var candidates = files
            .Select(path => new CursorFileCandidate(
                path,
                Normalize(Path.GetFileNameWithoutExtension(path))))
            .ToArray();

        foreach (var role in CursorRoles.All)
        {
            var match = FindMatch(role, candidates);
            if (match is not null)
            {
                result.FilesByRegistryName[role.RegistryName] = match.Path;
            }
        }

        if (!result.IsValid)
        {
            result.Warnings.Add("`*_通常.ani` / `.cur` に相当する通常カーソルが見つかりません。");
        }

        if (result.IsValid && result.MatchedRoleCount < CursorRoles.All.Count)
        {
            result.Warnings.Add(
                $"{result.MatchedRoleCount}/{CursorRoles.All.Count} 種類を検出しました。" +
                "不足分は Windows 標準カーソルで補われます。");
        }

        return result;
    }

    private static CursorFileCandidate? FindMatch(
        CursorRoleDefinition role,
        IReadOnlyList<CursorFileCandidate> candidates)
    {
        foreach (var rawAlias in role.FileAliases)
        {
            var alias = Normalize(rawAlias);

            var exact = candidates.FirstOrDefault(candidate => candidate.NormalizedStem == alias);
            if (exact is not null)
            {
                return exact;
            }

            var suffix = candidates.FirstOrDefault(candidate =>
                candidate.NormalizedStem.EndsWith("_" + alias, StringComparison.Ordinal) ||
                candidate.NormalizedStem.EndsWith("-" + alias, StringComparison.Ordinal) ||
                candidate.NormalizedStem.EndsWith(" " + alias, StringComparison.Ordinal));

            if (suffix is not null)
            {
                return suffix;
            }
        }

        // 以前の BAT でも Help だけは「ヘル」を含むファイルを予備検索していた。
        // 配布元によって「ヘルプ選択」など末尾表記が揺れるため、その互換処理を残す。
        if (string.Equals(role.RegistryName, "Help", StringComparison.OrdinalIgnoreCase))
        {
            return candidates.FirstOrDefault(candidate =>
                candidate.NormalizedStem.Contains("ヘル", StringComparison.Ordinal));
        }

        return null;
    }

    private static string Normalize(string value)
    {
        return value.Normalize(NormalizationForm.FormKC).Trim().ToLowerInvariant();
    }

    private sealed record CursorFileCandidate(string Path, string NormalizedStem);
}
