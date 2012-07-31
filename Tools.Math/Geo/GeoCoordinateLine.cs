﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tools.Math.Geo
{
    /// <summary>
    /// Class representing a geo coordinate line.
    /// </summary>
    [Serializable]
    public class GeoCoordinateLine : GenericLineF2D<GeoCoordinate>
    {
        /// <summary>
        /// Creates a geo coordinate line.
        /// </summary>
        /// <param name="point1"></param>
        /// <param name="point2"></param>
        public GeoCoordinateLine(
            GeoCoordinate point1,
            GeoCoordinate point2)
            :base(point1,point2)
        {

        }

        /// <summary>
        /// Creates a geo coordinate line.
        /// </summary>
        /// <param name="point1"></param>
        /// <param name="point2"></param>
        public GeoCoordinateLine(
            GeoCoordinate point1,
            GeoCoordinate point2,
            bool is_segment1,
            bool is_segment2)
            : base(point1, point2, is_segment1, is_segment2)
        {

        }

        #region Factory

        /// <summary>
        /// Creates a new point.
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        protected override GeoCoordinate CreatePoint(double[] values)
        {
            return new GeoCoordinate(values);
        }

        /// <summary>
        /// Creates a new line.
        /// </summary>
        /// <param name="point1"></param>
        /// <param name="point2"></param>
        /// <param name="is_segment1"></param>
        /// <param name="is_segment2"></param>
        /// <returns></returns>
        protected override GenericLineF2D<GeoCoordinate> CreateLine(
            GeoCoordinate point1,
            GeoCoordinate point2,
            bool is_segment1,
            bool is_segment2)
        {
            return new GeoCoordinateLine(point1, point2, is_segment1, is_segment2);
        }

        /// <summary>
        /// Creates a new rectangle
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        protected override GenericRectangleF2D<GeoCoordinate> CreateRectangle(GeoCoordinate[] points)
        {
            return new GeoCoordinateBox(points);
        }

        /// <summary>
        /// Creates a new polygon.
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        protected override GenericPolygonF2D<GeoCoordinate> CreatePolygon(GeoCoordinate[] points)
        {
            return new GeoCoordinatePolygon(points);
        }

        #endregion
    }
}