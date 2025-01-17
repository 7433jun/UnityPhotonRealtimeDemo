﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright company="Photon Engine">Photon Engine 2024</copyright>
// <summary>Calling some methods in intervals.</summary>
// <author>developer@photonengine.com</author>
// --------------------------------------------------------------------------------------------------------------------


namespace Photon.Utils
{
    using System;


    /// <summary>
    /// Helper class to time execution of tasks (e.g. in a thread that runs independently). ShouldExecute becomes true after the Interval passed. Call Reset to start over.
    /// </summary>
    /// <remarks>
    /// An interval can be overridden, when you set ShouldExecute to true.
    /// Call Reset after execution of whatever you do to re-enable the TimeKeeper (ShouldExecute becomes false until interval passed).
    /// Being based on Environment.TickCount, this is not very precise but cheap.
    /// </remarks>
    public class TimeKeeper
    {
        private int lastExecutionTime = Environment.TickCount;
        private bool shouldExecute;

        /// <summary>Interval in which ShouldExecute should be true (and something is executed).</summary>
        public int Interval { get; set; }

        /// <summary>A disabled TimeKeeper never turns ShouldExecute to true. Reset won't affect IsEnabled!</summary>
        public bool IsEnabled { get; set; }

        /// <summary>Turns true of the time interval has passed (after reset or creation) or someone set ShouldExecute manually.</summary>
        /// <remarks>Call Reset to start a new interval.</remarks>
        public bool ShouldExecute
        {
            get { return (this.IsEnabled && (this.shouldExecute || (Environment.TickCount - this.lastExecutionTime > this.Interval))); }
            set { this.shouldExecute = value; }
        }

        /// <summary>
        /// Creates a new TimeKeeper and sets it's interval.
        /// </summary>
        /// <param name="interval"></param>
        public TimeKeeper(int interval)
        {
            this.IsEnabled = true;
            this.Interval = interval;
        }

        /// <summary>ShouldExecute becomes false and the time interval is refreshed for next execution.</summary>
        /// <remarks>Does not affect IsEnabled.</remarks>
        public void Reset()
        {
            this.shouldExecute = false;
            this.lastExecutionTime = Environment.TickCount;
        }
    }
}
