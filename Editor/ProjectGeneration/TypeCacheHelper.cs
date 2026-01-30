/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Unity Technologies.
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace Microsoft.Unity.VisualStudio.Editor
{
    internal static class TypeCacheHelper
    {
        internal static IEnumerable<MethodInfo> GetPostProcessorCallbacks(string name)
        {
            return TypeCache
                .GetTypesDerivedFrom<AssetPostprocessor>()
                .Where(t => t.Assembly.GetName().Name != KnownAssemblies.Bridge)
                .Select(t => t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                .Where(m => m != null);
        }
    }
}