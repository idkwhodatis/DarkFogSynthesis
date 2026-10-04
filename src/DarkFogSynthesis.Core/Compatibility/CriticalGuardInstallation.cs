using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkFogSynthesis.Core.Compatibility
{
    /// <summary>An independent critical patch and its exact ownership/signature verification.</summary>
    public sealed class CriticalGuardStep
    {
        public string Name { get; }
        public Action Install { get; }
        public Action Verify { get; }

        public CriticalGuardStep(string name, Action install, Action verify)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Install = install ?? throw new ArgumentNullException(nameof(install));
            Verify = verify ?? throw new ArgumentNullException(nameof(verify));
        }
    }

    /// <summary>Attempt every independent barrier, keep survivors, and refuse initialization on any failure.</summary>
    public static class CriticalGuardInstallation
    {
        public static void InstallAndVerify(IEnumerable<CriticalGuardStep> steps) => Run(steps, true);
        public static void Verify(IEnumerable<CriticalGuardStep> steps) => Run(steps, false);

        private static void Run(IEnumerable<CriticalGuardStep> steps, bool install)
        {
            if (steps == null) throw new ArgumentNullException(nameof(steps));
            var all = steps.ToArray();
            if (all.Length == 0 || all.Any(step => step == null))
                throw new ArgumentException("Critical guard coverage must be nonempty and inspectable.", nameof(steps));
            var errors = new List<Exception>();
            if (install)
                foreach (var step in all)
                    try { step.Install(); }
                    catch (Exception error) { errors.Add(new InvalidOperationException("Critical guard installation failed: " + step.Name, error)); }
            foreach (var step in all)
                try { step.Verify(); }
                catch (Exception error) { errors.Add(new InvalidOperationException("Critical guard coverage is unverified: " + step.Name, error)); }
            if (errors.Count != 0)
                throw new AggregateException("Critical load/session/resume/save protection is incomplete. Do not load, resume or save; quit manually and correct the installation. " +
                    "Successfully installed guards were retained, but missing native hooks cannot be guaranteed. " +
                    string.Join("; ", errors.Select(error => error.Message)), errors);
        }
    }
}
