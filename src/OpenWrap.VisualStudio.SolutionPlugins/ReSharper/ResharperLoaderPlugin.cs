using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OpenFileSystem.IO;
using OpenWrap.Reflection;
using OpenWrap.Resources;
using OpenWrap.Services;

namespace OpenWrap.SolutionPlugins.VisualStudio.ReSharper
{
    public class ReSharperLoaderPlugin : IDisposable
    {
        const int MAX_RETRIES = 50;

        static readonly IDictionary<Version, string> VersionToLoaders = new Dictionary<Version, string>
        {
            { new Version("4.5.1288.2"), "OpenWrap.Resharper.PluginManager, OpenWrap.Resharper.450" },
            { new Version("5.0.1659.36"), "OpenWrap.Resharper.PluginManager, OpenWrap.Resharper.500" },
            { new Version("5.1.1727.12"), "OpenWrap.Resharper.PluginManager, OpenWrap.Resharper.510" },
            { new Version("6.0.2162.902"), "OpenWrap.Resharper.PluginManager, OpenWrap.Resharper.600" }
        };

        readonly IFileSystem _fileSystem;

        readonly IDirectory _rootAssemblyCacheDir;
        object _pluginManager;
        int _retries;

        public ReSharperLoaderPlugin() : this(ServiceLocator.GetService<IFileSystem>())
        {
        }

        public ReSharperLoaderPlugin(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
            _rootAssemblyCacheDir = _fileSystem.GetDirectory(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
                .GetDirectory("openwrap")
                .GetDirectory("VisualStudio")
                .GetDirectory("AssemblyCache")
                .MustExist();

            var vsAppDomain = AppDomain.CurrentDomain.GetData("openwrap.vs.appdomain") as AppDomain;

            if (vsAppDomain == null) return;

            DetectResharperLoading(vsAppDomain);
        }

        public void Dispose()
        {
            var disposer = _pluginManager as IDisposable;
            if (disposer != null) disposer.Dispose();
        }

        static Type LoadTypeFromVersion(Version resharperAssembly)
        {
            var typeName = (from version in VersionToLoaders.Keys
                            orderby version descending
                            where resharperAssembly >= version
                            select VersionToLoaders[version]).FirstOrDefault();
            return typeName != null ? Type.GetType(typeName, false) : null;
        }

        IFile CopyAndSign(string assemblyLocation)
        {
            var sourceAssemblyFile = _fileSystem.GetFile(assemblyLocation);
            // cheat because we know that the package will *always* be in /name-0.0.0.0/solution/xxxx
            var cacheDir = _rootAssemblyCacheDir.GetDirectory(sourceAssemblyFile.Parent.Parent.Name);
            if (cacheDir.Exists) return cacheDir.GetFile(sourceAssemblyFile.Name);
            var destinationAssemblyFile = cacheDir.GetFile(sourceAssemblyFile.Name);
            var now = DateTime.UtcNow;
            var build = (int)(now - new DateTime(2011, 06, 28)).TotalDays;

            var revision = (int)(now - new DateTime(now.Year, now.Month, now.Day)).TotalSeconds;

            sourceAssemblyFile.Sign(destinationAssemblyFile, new StrongNameKeyPair(Keys.openwrap), new Version(99, 99, build, revision));
            return destinationAssemblyFile;
        }

        void DetectResharperLoading(AppDomain vsAppDomain)
        {
            Assembly resharperAssembly;
            try
            {
                resharperAssembly = vsAppDomain.Load("JetBrains.Platform.ReSharper.Shell");
            }
            catch
            {
                resharperAssembly = null;
            }
            if (resharperAssembly == null)
            {
                if (_retries++ <= MAX_RETRIES)
                {
                    OpenWrapOutput.Write("ReSharper not found, try {0}/{1}", _retries, MAX_RETRIES);
                }

                return;
            }
            var resharperVersion = resharperAssembly.GetName().Version;
            var pluginManagerType = LoadTypeFromVersion(resharperVersion);
            if (pluginManagerType == null) return;

            var assemblyLocation = pluginManagerType.Assembly.Location;
            var destinationAssemblyFile = CopyAndSign(assemblyLocation);

            _pluginManager = vsAppDomain.CreateInstanceFromAndUnwrap(destinationAssemblyFile.Path, pluginManagerType.FullName);
            //_pluginManager = (IDisposable)vsAppDomain.CreateInstanceFromAndUnwrap(sourceAssemblyFile.Path, pluginManagerType.FullName);
        }
    }
}