using JBooth.MicroVerseCore.Browser;
using JBooth.MicroVerseCore;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static JBooth.MicroVerseCore.Browser.Package;

namespace Rowlan.Genesis
{
    public class ContentBrowserManager
    {

        private List<PackageSelection> filteredCollectionPackages = null;
        private List<ContentCollection> installedCollections = null;

        /// <summary>
        /// Get a content browser instance
        /// </summary>
        /// <returns>The first found content browser</returns>
        public static ContentBrowser GetContentBrowser()
        {
            ContentBrowser browser = null;

            // check if browser is open
            ContentBrowser[] browsers = Resources.FindObjectsOfTypeAll<ContentBrowser>();

            // browser not found, show message
            if (browsers.Length == 0)
            {
                return null;
            }
            // browser found
            else
            {
                // pick first browser found
                browser = browsers[0];
            }

            return browser;
        }

        public static List<T> LoadAllInstances<T>() where T : ScriptableObject
        {
            return AssetDatabase.FindAssets($"t: {typeof(T).Name}").ToList()
                        .Select(AssetDatabase.GUIDToAssetPath)
                        .Select(AssetDatabase.LoadAssetAtPath<T>)
                        .ToList();
        }

        public List<PackageSelection> GetFilteredCollectionPackages()
        {
            return filteredCollectionPackages;
        }
        public void LoadHeightContentCollections(bool forceReload)
        {
            if (filteredCollectionPackages != null && !forceReload)
                return;

            List<BrowserContent> allContent = LoadAllInstances<BrowserContent>();

            // installed collections
            installedCollections = allContent
            .Where(x => x.contentType == ContentType.Height)
            .Where(x => x is ContentCollection)
            .Cast<ContentCollection>()
            .ToList();

            // sort
            installedCollections = installedCollections.OrderByDescending(x => x.GetType().Name).ThenBy(x => x.packName).ToList();

            // packages
            filteredCollectionPackages = new List<PackageSelection>();
            foreach (ContentCollection c in installedCollections)
            {
                PackageSelection package = new PackageSelection(c.id, c.packName, c.contentType, PackageType.Collection);

                if (!filteredCollectionPackages.Contains(package))
                {
                    filteredCollectionPackages.Add(package);
                }

            }
        }

        public Texture2D GetRandomHeightMapFromContentCollection()
        {
            List<ContentData> contentDataList = new List<ContentData>();

            foreach (PackageSelection selectedPackage in filteredCollectionPackages)
            {
                if (!selectedPackage.selected)
                    continue;

                List<ContentCollection> selectedCollections = installedCollections.Where(x => x.packName == selectedPackage.packName).ToList();

                foreach (ContentCollection contentCollection in selectedCollections)
                {
                    contentDataList.AddRange(contentCollection.contents);
                }

            }

            if (contentDataList.Count == 0)
                return null;

            int randomIndex = Random.Range(0, contentDataList.Count - 1);
            ContentData contentData = contentDataList[randomIndex];

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(new GUID(contentData.stamp)));

            return texture;
        }

        public void SetAllPackageSelections( bool selected)
        {
            filteredCollectionPackages.ForEach(x => x.selected = selected);
        }

        public bool IsAnyPackageSelected()
        {
            foreach (PackageSelection selectedPackage in filteredCollectionPackages)
            {
                if (selectedPackage.selected)
                    return true;
            }

            return false;
        }

        public class PackageSelection : Package
        {
            public bool selected = false;

            public PackageSelection(string id, string packName, ContentType contentType, PackageType packageType) : base(id, packName, contentType, packageType)
            {
            }
        }
    }
}
