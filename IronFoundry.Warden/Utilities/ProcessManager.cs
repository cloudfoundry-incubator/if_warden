﻿namespace IronFoundry.Warden.Utilities
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using Containers;
    using System.Linq;
    using NLog;

    public class ProcessManager
    {
        private readonly Logger log = LogManager.GetCurrentClassLogger();
        private readonly ConcurrentDictionary<int, IProcess> processes = new ConcurrentDictionary<int, IProcess>();
        private readonly string containerUser;

        private readonly Func<Process, bool> processMatchesUser;

        public ProcessManager(string containerUser)
        {
            if (containerUser == null)
            {
                throw new ArgumentNullException("containerUser");
            }
            this.containerUser = containerUser;

            this.processMatchesUser = (process) =>
                {
                    string processUser = process.GetUserName();
                    return processUser == containerUser && !process.HasExited;
                };
        }

        public bool HasProcesses
        {
            get { return processes.Count > 0; }
        }


        public void AddProcess(IProcess process)
        {
            if (processes.TryAdd(process.Id, process))
            {
                process.Exited += process_Exited;
            }
            else
            {
                throw new InvalidOperationException(
                    String.Format("Process '{0}' already added to process manager for user '{1}'", process.Id, containerUser));
            }
        }

        public void AddProcess(Process process)
        {
            AddProcess(new RealProcessWrapper(process));
        }

        public void RestoreProcesses()
        {
            var allProcesses = Process.GetProcesses();
            allProcesses.Foreach(log, (p) => processMatchesUser(p),
                (p) =>
                {
                    var wrappedProcess = new RealProcessWrapper(p);
                    if (processes.TryAdd(wrappedProcess.Id, wrappedProcess))
                    {
                        log.Trace("Added process with PID '{0}' to container with user '{1}'", wrappedProcess.Id, containerUser);
                    }
                    else
                    {
                        log.Trace("Could NOT add process with PID '{0}' to container with user '{1}'", wrappedProcess.Id, containerUser);
                    }
                });
        }

        public void StopProcesses()
        {
            // TODO once job objects are working, we shouldn't need this.
            var processList = processes.Values.ToListOrNull();
            processList.Foreach(log, (p) => !p.HasExited, (p) => p.Kill());
            processList.Foreach(log, (p) => RemoveProcess(p.Id));

            var allProcesses = Process.GetProcesses();
            allProcesses.Foreach(log, (p) => processMatchesUser(p), (p) => p.Kill());
        }

        private void process_Exited(object sender, EventArgs e)
        {
            var process = (IProcess)sender;
            int pid = process.Id;
            process.Exited -= process_Exited;

            log.Trace("Process exited PID '{0}' exit code '{1}'", process.Id, process.ExitCode);

            RemoveProcess(pid);
        }

        private void RemoveProcess(int pid)
        {
            IProcess removed;
            if (processes.ContainsKey(pid) && !processes.TryRemove(pid, out removed))
            {
                log.Warn("Could not remove process '{0}' from collection!", pid);
            }
        }

        public ProcessStats GetStats()
        {
            var results = new ProcessStats();
            if ( processes.Count == 0 ) { return results; }

            results.TotalProcessorTime = processes.Values.Select(p => p.TotalProcessorTime).Aggregate<TimeSpan>((ag, next) => ag + next);

            return results;
        }

        class RealProcessWrapper : IProcess
        {
            Process process;
            public event EventHandler Exited;

            public RealProcessWrapper(Process process)
            {
                this.process = process;
                process.Exited += (o, e) => { this.OnExited(); };
            }

            public int Id
            {
                get { return process.Id; }
            }


            public int ExitCode
            {
                get { return process.ExitCode; }
            }

            public bool HasExited
            {
                get { return process.HasExited; }
            }

            public TimeSpan TotalProcessorTime
            {
                get { return process.TotalProcessorTime; }
            }

            public void Kill()
            {
                process.Kill();
            }

            protected virtual void OnExited()
            {
                var handlers = Exited;
                if (handlers != null)
                {
                    handlers.Invoke(this, EventArgs.Empty);
                }
            }

        }
    }

    public class ProcessStats
    {
        public TimeSpan TotalProcessorTime { get; set; }
    }
}
