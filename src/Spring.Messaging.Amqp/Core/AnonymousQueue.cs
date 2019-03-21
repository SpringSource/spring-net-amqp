﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AnonymousQueue.cs" company="The original author or authors.">
//   Copyright 2002-2012 the original author or authors.
//   
//   Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with
//   the License. You may obtain a copy of the License at
//   
//   https://www.apache.org/licenses/LICENSE-2.0
//   
//   Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on
//   an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the
//   specific language governing permissions and limitations under the License.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

#region Using Directives
using System;
using System.Collections;
#endregion

namespace Spring.Messaging.Amqp.Core
{
    /// <summary>
    /// An anonymous queue.
    /// </summary>
    /// <author>Dave Syer</author>
    /// <author>Joe Fitzgerald (.NET)</author>
    public class AnonymousQueue : Queue
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AnonymousQueue"/> class.
        /// </summary>
        public AnonymousQueue() : this(null) { }

        /// <summary>Initializes a new instance of the <see cref="AnonymousQueue"/> class.</summary>
        /// <param name="arguments">The arguments.</param>
        public AnonymousQueue(IDictionary arguments) : base(Guid.NewGuid().ToString(), false, true, true, arguments) { }
    }
}
