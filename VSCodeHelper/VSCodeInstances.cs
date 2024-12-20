// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace Flow.Plugin.VSCodeWorkspaces.VSCodeHelper
{
    public static class VSCodeInstances
    {
        private static string _systemPath = string.Empty;

        private static readonly string _userAppDataPath = Environment.GetEnvironmentVariable("AppData");

        public static List<VSCodeInstance> Instances { get; set; } = new();

        private static BitmapImage Bitmap2BitmapImage(Bitmap bitmap)
        {
            using (var memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Png);
                memory.Position = 0;

                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();

                return bitmapImage;
            }
        }

        private static Bitmap BitmapOverlayToCenter(Bitmap bitmap1, Bitmap overlayBitmap)
        {
            int bitmap1Width = bitmap1.Width;
            int bitmap1Height = bitmap1.Height;

            Bitmap overlayBitmapResized = new Bitmap(overlayBitmap, new System.Drawing.Size(bitmap1Width / 2, bitmap1Height / 2));

            float marginLeft = (float)((bitmap1Width * 0.7) - (overlayBitmapResized.Width * 0.5));
            float marginTop = (float)((bitmap1Height * 0.7) - (overlayBitmapResized.Height * 0.5));

            Bitmap finalBitmap = new Bitmap(bitmap1Width, bitmap1Height);
            using (Graphics g = Graphics.FromImage(finalBitmap))
            {
                g.DrawImage(bitmap1, System.Drawing.Point.Empty);
                g.DrawImage(overlayBitmapResized, marginLeft, marginTop);
            }

            return finalBitmap;
        }

        // Gets the executablePath and AppData foreach instance of VSCode
        public static void LoadVSCodeInstances()
        {
            if (_systemPath == Environment.GetEnvironmentVariable("PATH"))
                return;

            _systemPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            Instances = new List<VSCodeInstance>();

            // Find Windsurf installation
            var windsurfPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "windsurf", "Windsurf.exe");
            if (File.Exists(windsurfPath))
            {
                var windsurfAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "windsurf");
                if (Directory.Exists(windsurfAppData))
                {
                    var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    if (assemblyPath != null)
                    {
                        var resourcePath = Path.Combine(assemblyPath, "Images");
                        var instance = new VSCodeInstance
                        {
                            VSCodeVersion = VSCodeVersion.Windsurf,
                            ExecutablePath = windsurfPath,
                            AppData = windsurfAppData,
                            WorkspaceIconBitMap = Bitmap2BitmapImage((Bitmap)Image.FromFile(Path.Combine(resourcePath, "folder.png"))),
                            RemoteIconBitMap = Bitmap2BitmapImage((Bitmap)Image.FromFile(Path.Combine(resourcePath, "monitor.png")))
                        };
                        Instances.Add(instance);
                    }
                }
            }

            // Find VSCode installations
            var paths = _systemPath.Split(";").Where(x =>
                x.Contains("VS Code", StringComparison.OrdinalIgnoreCase) ||
                x.Contains("codium", StringComparison.OrdinalIgnoreCase) ||
                x.Contains("vscode", StringComparison.OrdinalIgnoreCase));
            foreach (var path in paths)
            {
                if (!Directory.Exists(path))
                    continue;

                var newPath = path;
                if (!Path.GetFileName(path).Equals("bin", StringComparison.OrdinalIgnoreCase))
                    newPath = Path.Combine(path, "bin");

                if (!Directory.Exists(newPath))
                    continue;

                var files = Directory.EnumerateFiles(newPath).Where(x =>
                    (x.Contains("code", StringComparison.OrdinalIgnoreCase) ||
                     x.Contains("codium", StringComparison.OrdinalIgnoreCase))
                    && !x.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)).ToArray();

                var iconPath = Path.GetDirectoryName(newPath);

                if (files.Length <= 0)
                    continue;

                var file = files[0];
                var version = string.Empty;

                var instance = new VSCodeInstance
                {
                    ExecutablePath = file,
                };

                if (file.EndsWith("code"))
                {
                    version = "Code";
                    instance.VSCodeVersion = VSCodeVersion.Stable;
                }
                else if (file.EndsWith("code-insiders"))
                {
                    version = "Code - Insiders";
                    instance.VSCodeVersion = VSCodeVersion.Insiders;
                }
                else if (file.EndsWith("code-exploration"))
                {
                    version = "Code - Exploration";
                    instance.VSCodeVersion = VSCodeVersion.Exploration;
                }
                else if (file.EndsWith("codium"))
                {
                    version = "VSCodium";
                    instance.VSCodeVersion = VSCodeVersion.Stable;
                }

                if (version == string.Empty)
                    continue;

                var portableData = Path.Join(iconPath, "data");
                instance.AppData = Directory.Exists(portableData) ? Path.Join(portableData, "user-data") : Path.Combine(_userAppDataPath, version);
                var iconVSCode = Path.Join(iconPath, $"{version}.exe");

                var bitmapIconVscode = Icon.ExtractAssociatedIcon(iconVSCode)?.ToBitmap();

                // workspace
                var folderIcon = (Bitmap)Image.FromFile(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "//Images//folder.png");
                instance.WorkspaceIconBitMap = Bitmap2BitmapImage(BitmapOverlayToCenter(folderIcon, bitmapIconVscode));

                // remote
                var monitorIcon = (Bitmap)Image.FromFile(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "//Images//monitor.png");

                instance.RemoteIconBitMap = Bitmap2BitmapImage(BitmapOverlayToCenter(monitorIcon, bitmapIconVscode));

                Instances.Add(instance);
            }
        }
    }
}