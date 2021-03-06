﻿// OsmSharp - OpenStreetMap (OSM) SDK
// Copyright (C) 2013 Abelshausen Ben
// 
// This file is part of OsmSharp.
// 
// OsmSharp is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
// 
// OsmSharp is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with OsmSharp. If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using OsmSharp.Math.Geo.Projections;
using OsmSharp.Math.Primitives;
using OsmSharp.UI.Renderer.Scene.Primitives;
using OsmSharp.UI.Renderer.Scene.Styles;

namespace OsmSharp.UI.Renderer.Scene.Simplification
{
    /// <summary>
    /// Resposible for merging similar objects together.
    /// </summary>
    public class Scene2DObjectMerger
    {
        /// <summary>
        /// Holds the epsilon.
        /// </summary>
        private float _epsilon;

        /// <summary>
        /// Creates a new scene object merger.
        /// </summary>
        public Scene2DObjectMerger()
        {
            _epsilon = 0.00001f;
        }

        /// <summary>
        /// Builds a merged version of the given scene object.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public Scene2D BuildMergedScene(Scene2D other)
        {
            Scene2D target = new Scene2D(new WebMercator(), other.GetZoomFactors().ToList(), true);
            for (int idx = 0; idx < other.GetZoomFactors().Length; idx++)
            {
                this.MergeObjects(target, other, idx);
            }
            return target;
        }

        /// <summary>
        /// Merges objects from the given scene for the given zoom level.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="source"></param>
        /// <param name="idx"></param>
        private void MergeObjects(Scene2D target, Scene2D source, int idx)
        {
            Dictionary<Scene2D.ScenePoints, Scene2DStylesSet> lines = new Dictionary<Scene2D.ScenePoints, Scene2DStylesSet>();
            Dictionary<PointF2D, HashSet<Scene2D.ScenePoints>> endpoints = new Dictionary<PointF2D, HashSet<Scene2D.ScenePoints>>();
            Dictionary<uint, SceneObject> sceneObjects = source.GetSceneObjectsAt(idx);
            foreach (var sceneObject in sceneObjects)
            {
                if (sceneObject.Value.Enum == SceneObjectType.LineObject)
                { // the scene object is a line object.
                    var sceneLineObject = sceneObject.Value as SceneLineObject;
                    Scene2D.ScenePoints scenePoints = source.GetPoints(sceneLineObject.GeoId);
                    Scene2DStylesSet stylesSet = null;
                    if (!lines.TryGetValue(scenePoints, out stylesSet))
                    { // create styles set.
                        stylesSet = new Scene2DStylesSet();
                        lines.Add(scenePoints, stylesSet);
                    }
                    stylesSet.AddStyleLine(sceneLineObject.StyleId);

                    //var sceneLineObject = (sceneObject.Value as SceneLineObject);
                    //Scene2D.ScenePoints scenePoints = source.GetPoints(sceneLineObject.GeoId);
                    //StyleLine styleLine = source.GetStyleLine(sceneLineObject.StyleId);

                    //uint? pointsId = target.AddPoints(scenePoints.X, scenePoints.Y);
                    //if (pointsId.HasValue)
                    //{
                    //    target.AddStyleLine(pointsId.Value, styleLine.Layer, styleLine.MinZoom, styleLine.MaxZoom,
                    //        styleLine.Color, styleLine.Width, styleLine.LineJoin, styleLine.Dashes);
                    //}
                }
                else if (sceneObject.Value.Enum == SceneObjectType.LineTextObject)
                {
                    var sceneLineTextObject = sceneObject.Value as SceneLineTextObject;
                    Scene2D.ScenePoints scenePoints = source.GetPoints(sceneLineTextObject.GeoId);
                    Scene2DStylesSet stylesSet = null;
                    if (!lines.TryGetValue(scenePoints, out stylesSet))
                    { // create styles set.
                        stylesSet = new Scene2DStylesSet();
                        lines.Add(scenePoints, stylesSet);
                    }
                    stylesSet.AddStyleLineText(sceneLineTextObject.StyleId, sceneLineTextObject.TextId);

                    //var sceneLineTextObject = (sceneObject.Value as SceneLineTextObject);
                    //Scene2D.ScenePoints scenePoints = source.GetPoints(sceneLineTextObject.GeoId);
                    //StyleText styleText = source.GetStyleText(sceneLineTextObject.StyleId);
                    //string text = source.GetText(sceneLineTextObject.TextId);

                    //uint? pointsId = target.AddPoints(scenePoints.X, scenePoints.Y);
                    //if (pointsId.HasValue)
                    //{
                    //    target.AddStyleLineText(pointsId.Value, styleText.Layer, styleText.MinZoom, styleText.MaxZoom,
                    //        styleText.Color, styleText.Size, text, styleText.Font, styleText.HaloColor, styleText.HaloRadius);
                    //}
                }
                else if (sceneObject.Value.Enum == SceneObjectType.IconObject)
                {
                    throw new NotSupportedException("Icons not yet supported!");
                    //var sceneIconObject = (sceneObject.Value as SceneIconObject);
                    //Scene2D.ScenePoint scenePoint = source.GetPoint(sceneIconObject.GeoId);
                    //source.GetStyleIcon(
                    //target.AddIcon(target.AddPoint(scenePoint.X, scenePoint.Y);
                }
                else if (sceneObject.Value.Enum == SceneObjectType.PointObject)
                {
                    var scenePointObject = (sceneObject.Value as ScenePointObject);
                    Scene2D.ScenePoint scenePoint = source.GetPoint(scenePointObject.GeoId);
                    StylePoint stylePoint = source.GetStylePoint(scenePointObject.StyleId);

                    target.AddStylePoint(target.AddPoint(scenePoint.X, scenePoint.Y), stylePoint.Layer, stylePoint.MinZoom, stylePoint.MaxZoom,
                        stylePoint.Color, stylePoint.Size);
                }
                else if (sceneObject.Value.Enum == SceneObjectType.PolygonObject)
                {
                    var scenePolygonObject = (sceneObject.Value as ScenePolygonObject);
                    Scene2D.ScenePoints scenePoints = source.GetPoints(sceneObject.Value.GeoId);
                    StylePolygon stylePolygon = source.GetStylePolygon(sceneObject.Value.StyleId);

                    uint? pointsId = target.AddPoints(scenePoints.X, scenePoints.Y);
                    if (pointsId.HasValue)
                    {
                        target.AddStylePolygon(pointsId.Value, stylePolygon.Layer, stylePolygon.MinZoom, stylePolygon.MaxZoom,
                            stylePolygon.Color, stylePolygon.Width, stylePolygon.Fill);
                    }
                }
                else if (sceneObject.Value.Enum == SceneObjectType.TextObject)
                {
                    var sceneTextObject = (sceneObject.Value as SceneTextObject);
                    Scene2D.ScenePoint scenePoint = source.GetPoint(sceneObject.Value.GeoId);
                    StyleText styleText = source.GetStyleText(sceneTextObject.StyleId);
                    string text = source.GetText(sceneTextObject.TextId);

                    target.AddText(target.AddPoint(scenePoint.X, scenePoint.Y), styleText.Layer, styleText.MinZoom, styleText.MaxZoom,
                        styleText.Size, text, styleText.Color, styleText.HaloColor, styleText.HaloRadius, styleText.Font);
                }
            }

            // loop until there are no more candidates.
            int totalLines = lines.Count;
            float latestProgress = 0;
            while (lines.Count > 0)
            {
                var line = lines.First();
                lines.Remove(line.Key);

                // report progress.
                float progress = (float)System.Math.Round((((double)(totalLines - lines.Count) / (double)totalLines) * 100));
                if (progress != latestProgress)
                {
                    OsmSharp.Logging.Log.TraceEvent("SceneSerializer", OsmSharp.Logging.TraceEventType.Information,
                        "Merging lines ({1}/{2})... {0}%", progress, totalLines - lines.Count, totalLines);
                    latestProgress = progress;
                }

                // copy the coordinates to lists.
                double[] x = line.Key.X.Clone() as double[];
                double[] y = line.Key.Y.Clone() as double[];

                // find a matching line.
                int mergeCount = 1;
                Scene2D.ScenePoints found;
                MatchPosition foundPosition = this.FindMatch(lines, x, y, line.Value, out found);
                while (found != null)
                { // TODO: keep expanding and duplicating until not possible anymore.
                    // remove the found line.
                    lines.Remove(found);

                    // add the line.
                    int lengthBefore = x.Length;
                    Array.Resize(ref x, x.Length + found.X.Length - 1);
                    Array.Resize(ref y, y.Length + found.Y.Length - 1);

                    switch (foundPosition)
                    {
                        case MatchPosition.FirstFirst:
                            found.X.InsertToReverse(1, x, 0, found.X.Length - 1);
                            found.Y.InsertToReverse(1, y, 0, found.Y.Length - 1);
                            break;
                        case MatchPosition.FirstLast:
                            found.X.InsertTo(0, x, 0, found.X.Length - 1);
                            found.Y.InsertTo(0, y, 0, found.Y.Length - 1);
                            break;
                        case MatchPosition.LastFirst:
                            found.X.CopyTo(x, lengthBefore - 1);
                            found.Y.CopyTo(y, lengthBefore - 1);
                            break;
                        case MatchPosition.LastLast:
                            found.X.CopyToReverse(x, lengthBefore - 1);
                            found.Y.CopyToReverse(y, lengthBefore - 1);
                            break;
                    }

                    // select a new line.
                    foundPosition = this.FindMatch(lines, x, y, line.Value, out found);
                    mergeCount++;
                }

                // add the new points.
                uint? pointsId = target.AddPoints(x, y);

                // add points again with appropriate styles.
                if (pointsId.HasValue)
                {
                    foreach (var style in line.Value)
                    {
                        var scene2DStyleLine = (style as Scene2DStyleLine);
                        if (scene2DStyleLine != null)
                        {
                            StyleLine styleLine = source.GetStyleLine(scene2DStyleLine.StyleLineId);
                            target.AddStyleLine(pointsId.Value, styleLine.Layer, styleLine.MinZoom, styleLine.MaxZoom,
                                styleLine.Color, styleLine.Width, styleLine.LineJoin, styleLine.Dashes);
                            continue;
                        }
                        var scene2DStyleLineText = (style as Scene2DStyleLineText);
                        if (scene2DStyleLineText != null)
                        {
                            StyleText styleText = source.GetStyleLineText(scene2DStyleLineText.StyleLineTextId);
                            string text = source.GetText(scene2DStyleLineText.TextId);
                            target.AddStyleLineText(pointsId.Value, styleText.Layer, styleText.MinZoom, styleText.MaxZoom,
                                styleText.Color, styleText.Size, text, styleText.Font, styleText.HaloColor, styleText.HaloRadius);
                            continue;
                        }
                    }
                }
            }
        }

        public enum MatchPosition
        {
            None,
            FirstFirst,
            FirstLast,
            LastFirst,
            LastLast
        }

        /// <summary>
        /// Try and find matching lines.
        /// </summary>
        /// <param name="lines"></param>
        /// <param name="points"></param>
        /// <returns></returns>
        private MatchPosition FindMatch(Dictionary<Scene2D.ScenePoints, Scene2DStylesSet> lines, double[] x, double[] y, Scene2DStylesSet style, out Scene2D.ScenePoints found)
        {
            PointF2D first = new PointF2D(x[0], y[0]);
            PointF2D last = new PointF2D(x[x.Length - 1], y[y.Length - 1]);

            MatchPosition position = MatchPosition.None;
            found = null;
            foreach (var line in lines)
            {
                if (line.Value.Equals(style))
                {
                    // check first.
                    PointF2D potentialFirst = new PointF2D(line.Key.X[0], line.Key.Y[0]);
                    if (first.Distance(potentialFirst) < _epsilon)
                    {
                        found = line.Key;
                        position = MatchPosition.FirstFirst;
                        break;
                    }
                    if (last.Distance(potentialFirst) < _epsilon)
                    {
                        found = line.Key;
                        position = MatchPosition.LastFirst;
                        break;
                    }

                    PointF2D potentialLast = new PointF2D(line.Key.X[line.Key.X.Length - 1], line.Key.Y[line.Key.Y.Length - 1]);
                    if (first.Distance(potentialLast) < _epsilon)
                    {
                        found = line.Key;
                        position = MatchPosition.FirstLast;
                        break;
                    }
                    if (last.Distance(potentialLast) < _epsilon)
                    {
                        found = line.Key;
                        position = MatchPosition.LastLast;
                        break;
                    }
                }
            }
            return position;
        }
    }
}