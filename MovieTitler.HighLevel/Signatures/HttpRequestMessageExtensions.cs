﻿// Copyright (c) 2021, Unisys
//
// Adapted from NSign, used under the terms of the MIT license
// https://github.com/Unisys/NSign/commit/660b2412cd523ed175d387cf32f549065b3cc56f

using MovieTitler.Interfaces;
using NSign.Signatures;
using static NSign.Constants;

namespace MovieTitler.HighLevel.Signatures;

internal static class IHttpRequestExtensions
{
    public static string GetDerivedComponentValue(this IRequest request, DerivedComponent derivedComponent)
    {
        return derivedComponent.ComponentName switch
        {
            DerivedComponents.SignatureParams =>
                throw new NotSupportedException("The '@signature-params' component cannot be included explicitly."),
            DerivedComponents.Method =>
                request.Method.Method,
            DerivedComponents.TargetUri =>
                request.RequestUri.OriginalString,
            DerivedComponents.Authority =>
                request.RequestUri.Authority.ToLower(),
            DerivedComponents.Scheme =>
                request.RequestUri.Scheme.ToLower(),
            DerivedComponents.RequestTarget =>
                request.RequestUri.PathAndQuery,
            DerivedComponents.Path =>
                request.RequestUri.AbsolutePath,
            DerivedComponents.Query =>
                string.IsNullOrWhiteSpace(request.RequestUri.Query)
                    ? "?"
                    : request.RequestUri.Query,
            DerivedComponents.QueryParam =>
                throw new NotSupportedException("The '@query-param' component must have the 'name' parameter set."),
            DerivedComponents.Status =>
                throw new NotSupportedException("The '@status' component cannot be included in request signatures."),
            _ =>
                throw new NotSupportedException(
                    $"Non-standard derived signature component '{derivedComponent.ComponentName}' cannot be retrieved."),
        };
    }
}