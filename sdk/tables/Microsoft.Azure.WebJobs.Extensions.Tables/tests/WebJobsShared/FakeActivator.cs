﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
namespace Microsoft.Azure.WebJobs.Host.TestCommon
{
    public class FakeActivator : IJobActivator
    {
        public Dictionary<Type, object> _instances = new Dictionary<Type, object>();
        public FakeActivator(params object[] objs)
        {
            foreach (var obj in objs)
            {
                Add(obj);
            }
        }
        public void Add(object o)
        {
            _instances[o.GetType()] = o;
        }
        public T CreateInstance<T>()
        {
            return (T)_instances[typeof(T)];
        }
    }
}