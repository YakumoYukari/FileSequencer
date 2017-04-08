using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.Utils;

namespace FileSequencer
{
    internal class Program
    {
        private static bool _Recursive;
        private static bool _MoveAnimated;
        private static string _StartingPath;

        private static readonly string[] ImageExtensions = {".bmp", ".jpg", ".jpeg", ".tif", ".tiff", ".png", ".gif"};
        private static readonly string[] AnimatedExtensions = {".webm", ".avi", ".mpg", ".flv"};
        private const string AnimatedDirectoryName = "Animated";

        private static void Main(string[] Args)
        {
            ProcessArgs(Args);

            _StartingPath = Path.GetFullPath(".");
            AssertValidPath();
            var Files = GetApplicableFiles(_StartingPath);

            DisplayFilesFound(Files);

            DisplayAffectedFileTypes(Files);
            ConfirmStart();

            StartRenamer(_StartingPath);
        }

        private static void ProcessArgs(string[] Args)
        {
            if (Args.Contains("-help")) WriteHelp();
            _Recursive = Args.Contains("-r");
            _MoveAnimated = Args.Contains("-a");
        }

        private static void WriteHelp()
        {
            Console.WriteLine("Parameters:");
            Console.WriteLine("  -r: Recurse child folders");
            Console.WriteLine("  -a: Separate animated filetypes into a new directory");
        }

        private static void AssertValidPath()
        {
            int StartingPathDepth =
                _StartingPath.Count(c => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar);

            if (!_StartingPath.IsNullOrWhiteSpace() && StartingPathDepth >= 2) return;
            
            Console.WriteLine($"Bad starting path: [{_StartingPath}]");
            Environment.Exit(1);
        }

        private static List<string> GetApplicableFiles(string Dir)
        {
            return Directory.GetFiles(Dir)
                .Where(f => IsValidFileType(Path.GetExtension(f)))
                .ToList();
        }

        private static bool IsValidFileType(string Extension)
        {
            Extension = Extension.StartsWith(".") ? Extension : $".{Extension}";
            return AnimatedExtensions.Contains(Extension) ||
                   ImageExtensions.Contains(Extension);
        }

        private static void DisplayFilesFound(IReadOnlyCollection<string> Files)
        {
            Console.WriteLine();
            Console.WriteLine($"Files found: {Files.Count}");

            if (Files.Count != 0) return;

            Console.WriteLine("No files found!");
            Environment.Exit(0);
        }

        private static void ConfirmStart()
        {
            int AffectedFolders = 0;
            if (_Recursive)
                AffectedFolders = GetAffectedFolderCount(_StartingPath);

            Console.WriteLine();
            Console.WriteLine($"Working path: {_StartingPath}");

            bool Rename =
                Prompt.Bool("Rename files to sequential names?" +
                            (_Recursive
                                ? $" (RECURSIVE: {AffectedFolders} FOLDER{(AffectedFolders > 1 ? "S" : "")} AFFECTED!)"
                                : ""));
            if (Rename) return;

            Console.WriteLine("Process aborted!");
            Environment.Exit(0);
        }

        private static void DisplayAffectedFileTypes(IEnumerable<string> Files)
        {
            Files.Select(f => Path.GetExtension(f)?.ToLower() ?? ".unknown")
                            .GroupBy(x => x)
                            .Select(g => new { Type = g.Key, Count = g.Count() })
                            .ForEach(e => Console.WriteLine($"Extension: {e.Type}, Count: {e.Count}"));
        }

        private static int GetAffectedFolderCount(string StartingPath)
        {
            int Additional = 0;
            var Directories = Directory.GetDirectories(StartingPath)
                .Where(FolderNotCreatedByUs)
                .ToList();

            Directories.ForEach(Dir => Additional += GetAffectedFolderCount(Dir));
            return Additional + Directories.Count;
        }

        private static bool FolderNotCreatedByUs(string FolderPath)
        {
            return Path.GetFileName(FolderPath)?.TrimEnd('\\', '/') != AnimatedDirectoryName;
        }

        private static void StartRenamer(string Dir)
        {
            if (_Recursive)
            {
                Directory.GetDirectories(Dir)
                    .Where(FolderNotCreatedByUs)
                    .ForEach(StartRenamer);
            }

            RenameFiles(Dir);
        }

        private static void RenameFiles(string Dir)
        {
            var FilePaths = GetApplicableFiles(Dir);
            if (!FilePaths.Any()) return;

            var FullPaths = GetNonAnimatedFiles(FilePaths);
            RenameInCurrentDirectory(FullPaths, Dir);

            if (!_MoveAnimated) return;

            var AnimatedFiles = GetAnimatedFiles(FilePaths);
            string AnimatedDir = GetAnimatedFilesDirectory(Dir);

            RenameToAnimatedDirectory(AnimatedFiles, AnimatedDir);
        }

        private static string GetAnimatedFilesDirectory(string Dir)
        {
            string AnimatedDir = Path.Combine(Dir, AnimatedDirectoryName);
            if (Directory.Exists(AnimatedDir)) return AnimatedDir;

            Directory.CreateDirectory(AnimatedDir);

            return AnimatedDir;
        }

        private static void RenameInCurrentDirectory(IEnumerable<string> FullPaths, string CurrentDir)
        {
            int Current = 1;
            foreach (string FileToBeRenamed in FullPaths.Where(f => f.IsNotNullOrEmpty()))
            {
                string Ext = Path.GetExtension(FileToBeRenamed).ToLower();
                string NewPath = Path.Combine(CurrentDir, $"{Current++}{Ext}");
                SafeFileMove(FileToBeRenamed, NewPath);
            }
        }

        private static void RenameToAnimatedDirectory(IEnumerable<string> AnimatedFiles, string AnimatedDir)
        {
            int Current = 1;
            foreach (string FileToBeMoved in AnimatedFiles.Where(f => f.IsNotNullOrEmpty()))
            {
                string Ext = Path.GetExtension(FileToBeMoved).ToLower();
                string NewPath = Path.Combine(AnimatedDir, $"{Current++}{Ext}");
                SafeFileMove(FileToBeMoved, NewPath);
            }
        }

        private static void SafeFileMove(string From, string To)
        {
            if (Path.GetFullPath(From) == Path.GetFullPath(To)) return;
            try
            {
                File.Move(From, To);
            }
            catch
            {
                Console.WriteLine("Cannot move file:");
                Console.WriteLine($"  From : {From}");
                Console.WriteLine($"  To   : {To}");
                Environment.Exit(1);
            }
        }

        private static IEnumerable<string> GetNonAnimatedFiles(IEnumerable<string> FilePaths)
        {
            var ApplicableFiles = FilePaths
                .Select(Path.GetFullPath)
                .Where(f => !_MoveAnimated || !AnimatedExtensions.Contains(Path.GetExtension(f) ?? ".unknown"));

            return GetOrderedFiles(ApplicableFiles);
        }

        private static IEnumerable<string> GetAnimatedFiles(IEnumerable<string> FilePaths)
        {
            var ApplicableFiles = FilePaths
                .Select(Path.GetFullPath)
                .Where(f => AnimatedExtensions.Contains(Path.GetExtension(f) ?? ".unknown"));

            return GetOrderedFiles(ApplicableFiles);
        }
        
        private static IEnumerable<string> GetOrderedFiles(IEnumerable<string> FilePaths)
        {
            return FilePaths
                .OrderBy(f =>
                {
                    string Name = Path.GetFileNameWithoutExtension(f);
                    int OutVal;
                    return int.TryParse(Name, out OutVal) ? OutVal : int.MaxValue;
                });
        }
    }
}
