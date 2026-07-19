using System.Text;
using System.IO.Enumeration;
using CursorCycle.Domain;

namespace CursorCycle.Infrastructure;

public sealed class CursorFolderScanner
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".ani", ".cur" };

    private readonly CursorDetectionConfigStore _configStore = new();

    public CursorSetScanResult Scan(CursorPreset preset)
    {
        var folderPath = preset.FolderPath;
        var result = new CursorSetScanResult
        {
            FolderPath = folderPath
        };

        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            result.Warnings.Add("フォルダーが見つかりません。");
            return result;
        }

        var config = _configStore.Load();
        string[] files;
        try
        {
            var searchOption = config.SearchSubfolders
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;
            files = Directory.EnumerateFiles(folderPath, "*", searchOption)
                .Where(path => SupportedExtensions.Contains(Path.GetExtension(path)))
                .OrderByDescending(path => config.PreferAnimatedCursors &&
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
            config.PatternsByRegistryName.TryGetValue(role.RegistryName, out var patterns);
            var match = FindMatch(patterns ?? [], candidates);
            if (match is not null)
            {
                result.FilesByRegistryName[role.RegistryName] = match.Path;
            }
        }

        ApplyManualAssignments(preset, result);

        if (!result.IsValid)
        {
            result.Warnings.Add(
                "通常カーソルが見つかりません。検索設定または個別設定を確認してください。");
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
        IReadOnlyList<string> patterns,
        IReadOnlyList<CursorFileCandidate> candidates)
    {
        foreach (var rawPattern in patterns)
        {
            var pattern = Normalize(rawPattern);
            var match = candidates.FirstOrDefault(candidate =>
                FileSystemName.MatchesSimpleExpression(
                    pattern,
                    candidate.NormalizedStem,
                    ignoreCase: true));
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static void ApplyManualAssignments(
        CursorPreset preset,
        CursorSetScanResult result)
    {
        foreach (var assignment in preset.ManualFilesByRegistryName ??
                 new Dictionary<string, string>())
        {
            var role = CursorRoles.All.FirstOrDefault(candidate =>
                string.Equals(
                    candidate.RegistryName,
                    assignment.Key,
                    StringComparison.OrdinalIgnoreCase));
            if (role is null)
            {
                continue;
            }

            string resolvedPath;
            try
            {
                resolvedPath = ResolveManualPath(preset.FolderPath, assignment.Value);
            }
            catch (Exception exception)
            {
                result.Warnings.Add(
                    $"{role.DisplayName} の個別設定パスを読み込めません: {exception.Message}");
                continue;
            }
            if (!File.Exists(resolvedPath) ||
                !SupportedExtensions.Contains(Path.GetExtension(resolvedPath)))
            {
                result.Warnings.Add(
                    $"{role.DisplayName} の個別設定ファイルが見つからないため、自動検出を使用します: " +
                    assignment.Value);
                continue;
            }

            result.FilesByRegistryName[role.RegistryName] = resolvedPath;
            result.ManuallyAssignedRegistryNames.Add(role.RegistryName);
        }
    }

    private static string ResolveManualPath(string folderPath, string configuredPath)
    {
        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(folderPath, configuredPath));
    }

    private static string Normalize(string value)
    {
        return value.Normalize(NormalizationForm.FormKC).Trim().ToLowerInvariant();
    }

    private sealed record CursorFileCandidate(string Path, string NormalizedStem);
}
