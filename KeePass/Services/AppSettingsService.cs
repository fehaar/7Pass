﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using KeePass.IO;
using KeePass.IO.Utils;

namespace KeePass.Services
{
    internal static class AppSettingsService
    {
        private static readonly IsolatedStorageSettings _appSettings;

        private static readonly IList<Guid> _recents;

        public static string DownloadUrl
        {
            get
            {
                string value;
                return _appSettings.TryGetValue(Consts.KEY_URL, out value)
                    ? value : null;
            }
            set
            {
                _appSettings[Consts.KEY_URL] = value;
                _appSettings.Save();
            }
        }

        static AppSettingsService()
        {
            _recents = new List<Guid>();
            _appSettings = IsolatedStorageSettings
                .ApplicationSettings;
        }

        public static void AddRecent(Guid id)
        {
            _recents.Remove(id);
            _recents.Insert(0, id);

            while (_recents.Count > 10)
                _recents.RemoveAt(10);

            _appSettings[Consts.KEY_RECENTS] =
                _recents.ToArray();
        }

        public static void Clear()
        {
            KeyCache.Database = null;
        }

        public static void ClearRecents()
        {
            _recents.Clear();
            _appSettings[Consts.KEY_RECENTS] =
                _recents.ToArray();
        }

        public static void ClearPassword()
        {
            using (var store = IsolatedStorageFile
                .GetUserStoreForApplication())
            {
                if (store.FileExists(Consts.DECRYPTED))
                    store.DeleteFile(Consts.DECRYPTED);
            }
        }

        public static IList<Guid> GetRecents()
        {
            return _recents.ToArray();
        }

        public static bool HasDatabase()
        {
            using (var store = IsolatedStorageFile
                .GetUserStoreForApplication())
            {
                return store.FileExists(Consts.DATABASE);
            }
        }

        public static bool HasPassword()
        {
            using (var store = IsolatedStorageFile
                .GetUserStoreForApplication())
            {
                return store.FileExists(Consts.DECRYPTED);
            }
        }

        public static void LoadSettings()
        {
            LoadRecents();

            if (!HasPassword())
                return;

            using (var store = IsolatedStorageFile
                .GetUserStoreForApplication())
            {
                var db = new DbPersistentData
                {
                    Xml = ReadFile(store, Consts.DECRYPTED),
                    Protection = ReadFile(store, Consts.PROTECTION),
                };

                KeyCache.Database = DatabaseReader.Load(db);
            }
        }

        public static bool Open(string password,
            bool savePassword)
        {
            Clear();

            using (var store = IsolatedStorageFile
                .GetUserStoreForApplication())
            {
                if (!store.FileExists(Consts.DATABASE))
                    return false;

                try
                {
                    using (var fs = store.OpenFile(
                        Consts.DATABASE, FileMode.Open))
                    {
                        var xml = DatabaseReader.GetXml(fs, password);

                        if (savePassword)
                            Save(store, xml);

                        KeyCache.Database = DatabaseReader.Load(xml);
                        return true;
                    }
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        private static void LoadRecents()
        {
            object ids;
            if (!_appSettings.TryGetValue(Consts.KEY_RECENTS, out ids))
                return;

            _recents.Clear();
            foreach (var id in (Guid[])ids)
                _recents.Add(id);
        }

        private static byte[] ReadFile(IsolatedStorageFile store, string path)
        {
            using (var stream = store.OpenFile(path,
                FileMode.Open, FileAccess.Read))
            {
                using (var buffer = new MemoryStream())
                {
                    BufferEx.CopyStream(stream, buffer);
                    return buffer.ToArray();
                }
            }
        }

        private static void Save(IsolatedStorageFile store,
            DbPersistentData db)
        {
            var xml = db.Xml;
            using (var fs = store.CreateFile(Consts.DECRYPTED))
            {
                fs.Write(xml, 0, xml.Length);
                fs.Flush();
            }

            var protection = db.Protection;
            using (var fs = store.CreateFile(Consts.PROTECTION))
            {
                fs.Write(protection, 0, protection.Length);
                fs.Flush();
            }
        }
    }
}