using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Executor.WaveUI
{
    internal static class WaveAssets
    {
        private static readonly object CacheLock = new();
        private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Lazy<HashSet<string>> EmbeddedResourceIndex = new(BuildEmbeddedResourceIndex);
        private const int MaxCacheEntries = 256;

        internal static ImageSource? TryLoadIcon(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var key = name.Trim();

            lock (CacheLock)
            {
                if (Cache.TryGetValue(key, out var cached))
                {
                    return cached;
                }
            }

            var fileName = key + ".png";
            var fromFile = TryLoadFromFile(fileName);
            if (fromFile != null)
            {
                lock (CacheLock)
                {
                    if (Cache.Count >= MaxCacheEntries)
                    {
                        Cache.Clear();
                    }
                    Cache[key] = fromFile;
                }
                return fromFile;
            }

            var fromResource = TryLoadFromResource(fileName);
            lock (CacheLock)
            {
                if (Cache.Count >= MaxCacheEntries)
                {
                    Cache.Clear();
                }
                Cache[key] = fromResource;
            }
            return fromResource;
        }

        private static ImageSource? TryLoadFromFile(string fileName)
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var candidates = new[]
                {
                    Path.Combine(baseDir, "Assets", "WaveUI", fileName),
                    Path.Combine(baseDir, "Assets", "icons", "wave", fileName),
                    Path.Combine(baseDir, "assets", "icons", "wave", fileName),
                    Path.Combine(baseDir, "assets", "wave", fileName),
                };

                foreach (var path in candidates)
                {
                    if (!File.Exists(path))
                    {
                        continue;
                    }

                    return LoadBitmap(new Uri(path, UriKind.Absolute));
                }
            }
            catch
            {
            }

            return null;
        }

        private static ImageSource? TryLoadFromResource(string fileName)
        {
            // Avoid probing missing pack:// resources (each miss throws IOException in WPF).
            // We first consult the compiled .g.resources index and only request streams for existing keys.
            var keys = EmbeddedResourceIndex.Value;

            var candidates = new[]
            {
                $"Assets/WaveUI/{fileName}",
                $"assets/icons/wave/{fileName}",
                $"Assets/icons/wave/{fileName}",
                $"assets/wave/{fileName}",
            };

            foreach (var relative in candidates)
            {
                if (!keys.Contains(NormalizeResourceKey(relative)))
                {
                    continue;
                }

                var uriText = $"pack://application:,,,/{relative.Replace('\\', '/')}";
                try
                {
                    var info = Application.GetResourceStream(new Uri(uriText, UriKind.Absolute));
                    if (info?.Stream == null)
                    {
                        continue;
                    }

                    using var stream = info.Stream;
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = stream;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
                catch
                {
                }
            }

            return null;
        }

        private static HashSet<string> BuildEmbeddedResourceIndex()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var name = asm.GetName().Name;
                if (string.IsNullOrWhiteSpace(name))
                {
                    return set;
                }

                var resourceName = name + ".g.resources";
                using var stream = asm.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    return set;
                }

                using var reader = new ResourceReader(stream);
                foreach (DictionaryEntry entry in reader)
                {
                    if (entry.Key is string key)
                    {
                        set.Add(NormalizeResourceKey(key));
                    }
                }
            }
            catch
            {
            }

            return set;
        }

        private static string NormalizeResourceKey(string key)
        {
            return key.Replace('\\', '/').TrimStart('/');
        }

        private static ImageSource LoadBitmap(Uri uri)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = uri;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
    }
}
