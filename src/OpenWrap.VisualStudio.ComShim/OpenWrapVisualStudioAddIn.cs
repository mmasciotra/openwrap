﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting;
using System.Threading;
using EnvDTE;
using EnvDTE80;
using Extensibility;
using OpenWrap.Preloading;

namespace OpenWrap.VisualStudio.SolutionAddIn
{
    public abstract class OpenWrapVisualStudioAddIn : MarshalByRefObject, IDTExtensibility2
    {
        readonly string _progId;

        DTE _dte;
        OutputWindowPane _outputWindow;
        AppDomain _appDomain;
        ObjectHandle _loader;
        string _rootPath;
        public string DteVersion { get; private set; }

        public OpenWrapVisualStudioAddIn(string targetDteVersion, string progId)
        {
            _progId = progId;
            DteVersion = targetDteVersion;
        }

        protected EnvDTE.AddIn AddIn { get; set; }
        protected DTE2 Application { get; set; }

        public void OnAddInsUpdate(ref Array custom)
        {
        }

        public void OnBeginShutdown(ref Array custom)
        {
        }

        public void OnConnection(object application, ext_ConnectMode connectMode, object addInInst, ref Array custom)
        {
            var dte = application as DTE;
            if (dte == null) return;
            _dte = dte;

            if (dte.Version != DteVersion)
            {
                Notify("OpenWrap Visual Studio integration is not correct version, re-creating now.");
                dte.Solution.AddIns.OfType<EnvDTE.AddIn>().First(x => x.ProgID == _progId).Remove();
                if (dte.Version == "9.0")
                    dte.Solution.AddIns.Add(ComConstants.ADD_IN_PROGID_2008, ComConstants.ADD_IN_DESCRIPTION, ComConstants.ADD_IN_NAME, true);
                else if (dte.Version == "10.0")
                    dte.Solution.AddIns.Add(ComConstants.ADD_IN_PROGID_2010, ComConstants.ADD_IN_DESCRIPTION, ComConstants.ADD_IN_NAME, true);
                return;
            }
            _rootPath = GetRootLocation(dte.Solution.FullName);
            if (_rootPath == null) return;
            Notify("Root location: " + _rootPath);

            LoadAppDomain();
        }

        void LoadAppDomain()
        {
            Notify("Loading packages...");


            try
            {
                var packages = Preloader.GetPackageFolders(Preloader.RemoteInstall.None, _rootPath, null, "openwrap").ToArray();

                foreach (var package in packages) Notify("Loading package " + package);
                _appDomain = AppDomain.CreateDomain("OpenWrap Visual Studio Integration (default scope)");
                _appDomain.SetData("openwrap.vs.version", _dte.Version);
                _appDomain.SetData("openwrap.vs.currentdirectory", _rootPath);
                _appDomain.SetData("openwrap.vs.packages", packages.ToArray());
                
                //_appDomain.DomainUnload += HandleAppDomainChange;
                // replace that with the location in the codebase we just registered, ensuring the correct version is loaded
                var location = GetType().Assembly.Location;
                
                _loader = _appDomain.CreateInstanceFrom(location, typeof(AddInAppDomainManager).FullName);
                

            }
            catch(Exception e)
            {
                Notify("Could not load bootstrap packages.");
                Notify(e.ToString());
            }
        }
        public override object InitializeLifetimeService()
        {
            return null;
        }
        void HandleAppDomainChange(object sender, EventArgs e)
        {
            try
            {
                    Notify("Change detected");
                    LoadAppDomain();
            }catch
            {
                Notify("OpenWrap unloading.");
            }
        }

        string GetRootLocation(string fullName)
        {
            var curDir = new DirectoryInfo(Path.GetDirectoryName(fullName));
            do
            {
                if (curDir.GetFiles("*.wrapdesc").Length > 0) return curDir.FullName;
            } while ((curDir = curDir.Parent) != null);
            Notify("Could not locate descriptor file.");
            return null;
        }

        void Notify(string message)
        {
            Debug.WriteLine(message);
            if (_outputWindow == null)
            {
                var output = (OutputWindow)_dte.Windows.Item(EnvDTE.Constants.vsWindowKindOutput).Object;

                _outputWindow = output.OutputWindowPanes.Cast<OutputWindowPane>().FirstOrDefault(x => x.Name == "OpenWrap")
                    ?? output.OutputWindowPanes.Add("OpenWrap");

            }

            _outputWindow.OutputString(message + "\r\n");
        }

        public void OnDisconnection(ext_DisconnectMode removeMode, ref Array custom)
        {
            Notify("Unloading. Goodbye.");
            AppDomain.Unload(_appDomain);
        }

        public void OnStartupComplete(ref Array custom)
        {
        }
    }
    public class AddInAppDomainManager : MarshalByRefObject
    {
        
        public override object InitializeLifetimeService()
        {
            return null;
        }

        public AddInAppDomainManager()
        {
            var appDomain = AppDomain.CurrentDomain;
            ThreadPool.QueueUserWorkItem(state => Load((string)appDomain.GetData("openwrap.vs.version"),
                                                       (string)appDomain.GetData("openwrap.vs.currentdirectory"),
                                                       (string[])appDomain.GetData("openwrap.vs.packages")));
        }

        void Load(string vsVersion, string currentDirectory, string[] packagePaths)
        {
            var assemblies = Preloader.LoadAssemblies(packagePaths);
            Func<IDictionary<string, object>, int> runner = null;
            foreach(var asm in assemblies)
            {
                try
                {
                    var runnerType = (from type in asm.Key.GetExportedTypes()
                                  where type.Name.EndsWith("Runner")
                                  let mi = type.GetMethod("Main", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(IDictionary<string, object>) }, null)
                                  where mi != null
                                  select mi).FirstOrDefault();
                    if (runnerType != null)
                    {
                        runner = env => (int)runnerType.Invoke(null, new object[] { env });
                        break;
                    }
                }
                catch
                {
                }
            }
            if (runner == null) return;
            var info = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                    { "openwrap.cd", currentDirectory },
                    { "openwrap.shell.commandline", "start-solutionplugin" },
                    { "openwrap.shell.assemblies", assemblies.ToList() },
                    { "openwrap.shell.version", "1.1" },
                    { "openwrap.shell.type", "VisualStudio." + vsVersion }
            };
            
            runner(info);

        }
    }
}