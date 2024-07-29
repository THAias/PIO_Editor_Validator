﻿/* 
 * Copyright (c) 2024, Firely (info@fire.ly) and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://github.com/FirelyTeam/firely-validator-api/blob/main/LICENSE
 */

#nullable enable


using Hl7.Fhir.ElementModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Firely.Fhir.Validation
{
    internal static class IScopedNodeExtensions
    {
        /// <summary>
        /// Converts a <see cref="IScopedNode"/> to a <see cref="ITypedElement"/>.
        /// </summary>
        /// <param name="node">An <see cref="IScopedNode"/> node</param>
        /// <returns>An implementation of <see cref="ITypedElement"/></returns>
        /// <remarks>Be careful when using this method, the returned <see cref="ITypedElement"/> does not implement
        /// the methods <see cref="ITypedElement.Location"/> and <see cref="ITypedElement.Definition"/>.    
        /// </remarks>
        [Obsolete("WARNING! For internal API use only. Turning an IScopedNode into an ITypedElement will cause problems for" +
            "Location and Definitions. Those properties are not implemented using this method and can cause problems " +
            "elsewhere. Please don't use this method unless you know what you are doing.")]
        public static ITypedElement AsTypedElement(this IScopedNode node) => node switch
        {
            TypedElementToIScopedNodeToAdapter adapter => adapter.ScopedNode,
            ITypedElement ite => ite,
            _ => new ScopedNodeToTypedElementAdapter(node)
        };
        //node is ITypedElement ite ? ite : new ScopedNodeToTypedElementAdapter(node);

        public static ScopedNode ToScopedNode(this IScopedNode node) => node switch
        {
            TypedElementToIScopedNodeToAdapter adapter => adapter.ScopedNode,
            _ => throw new ArgumentException("The node is not a TypedElementToIScopedNodeToAdapter")
        };

        /// <summary>
        /// Returns the parent resource of this node, or null if this node is not part of a resource.
        /// </summary>
        /// <param name="nodes"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static IEnumerable<IScopedNode> Children(this IEnumerable<IScopedNode> nodes, string? name = null) =>
           nodes.SelectMany(n => n.Children(name));
    }
}

#nullable restore