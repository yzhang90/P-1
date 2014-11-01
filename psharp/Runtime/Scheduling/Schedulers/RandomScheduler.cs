﻿//-----------------------------------------------------------------------
// <copyright file="RandomScheduler.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation. All rights reserved.
// 
//      THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, 
//      EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES 
//      OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE.
// ----------------------------------------------------------------------------------
//      The example companies, organizations, products, domain names,
//      e-mail addresses, logos, people, places, and events depicted
//      herein are fictitious.  No association with any real company,
//      organization, product, domain name, email address, logo, person,
//      places, or events is intended or should be inferred.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.PSharp;
using Microsoft.PSharp.Scheduling;

namespace Microsoft.PSharp.Scheduling
{
    /// <summary>
    /// Class representing a random delay scheduler.
    /// </summary>
    public sealed class RandomScheduler : IScheduler
    {
        /// <summary>
        /// Randomly generates values.
        /// </summary>
        internal Random Randomizer;

        /// <summary>
        /// Default constructor of the RandomScheduler class.
        /// </summary>
        public RandomScheduler()
        {
            this.Randomizer = new Random(DateTime.Now.Second);
        }

        /// <summary>
        /// Returns the next machine ID to be scheduled.
        /// </summary>
        /// <param name="nextId">Next machine ID</param>
        /// <param name="machineIDs">Machine IDs</param>
        /// <returns>Boolean value</returns>
        bool IScheduler.TryGetNext(out int nextId, List<int> machineIDs)
        {
            var index = this.Randomizer.Next(0, machineIDs.Count);
            nextId = machineIDs[index];
            return true;
        }

        /// <summary>
        /// Returns true if the scheduler has finished.
        /// </summary>
        /// <returns>Boolean value</returns>
        bool IScheduler.HasFinished()
        {
            return false;
        }

        /// <summary>
        /// Resets the scheduler.
        /// </summary>
        void IScheduler.Reset()
        {
            this.Randomizer = new Random(DateTime.Now.Second);
        }
    }
}
