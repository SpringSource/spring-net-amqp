﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RabbitAdminAuthException.cs" company="The original author or authors.">
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
using Erlang.NET;
#endregion

namespace Spring.Messaging.Amqp.Rabbit.Admin
{
    /// <summary>
    /// A rabbit admin auth exception.
    /// </summary>
    public class RabbitAdminAuthException : OtpAuthException
    {
        /// <summary>Initializes a new instance of the <see cref="RabbitAdminAuthException"/> class.</summary>
        /// <param name="message">The message.</param>
        /// <param name="cause">The cause.</param>
        public RabbitAdminAuthException(string message, OtpAuthException cause) : base(message, cause) { }
    }
}
