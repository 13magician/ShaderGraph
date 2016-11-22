using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.RMGUI;

namespace RMGUI.GraphView
{
	public class Edge : GraphElement
	{
		const float k_EndPointRadius = 4.0f;
		const float k_InterceptWidth = 3.0f;

		public override void OnDataChanged()
		{
			base.OnDataChanged();
			this.Touch(ChangeType.Repaint);
		}

		// TODO lots of redundant code in here that could be factorized
		// TODO The tangents are calculated way to often. We should compute them on repaint only.

		protected static void GetTangents(Orientation orientation, Vector2 from, Vector2 to, out Vector3[] points, out Vector3[] tangents)
        {
            bool invert = orientation == Orientation.Horizontal ? from.x > to.x : from.y > to.y;
            float inverse = invert ? -1.0f : 1.0f;

            tangents = new Vector3[2];
            points = new Vector3[] { from, to };

            float minTangent = Mathf.Min(Vector3.Distance(from,to) * 0.25f, 100.0f);

            float weight = .5f;
            float weight2 = 1 - weight;

            if (orientation == Orientation.Horizontal)
            {
                tangents[0] = from + new Vector2((to.x - from.x) * weight * inverse + minTangent, 0);
                tangents[1] = to + new Vector2((to.x - from.x) * -weight2 * inverse - minTangent, 0);
            }
            else
            {
                tangents[0] = from + new Vector2(0, (to.y - from.y) * weight * inverse + minTangent);
                tangents[1] = to + new Vector2(0, (to.y - from.y) * -weight2 * inverse - minTangent);
            }
        }

		public override bool Overlaps(Rect rect)
		{
			// bounding box check succeeded, do more fine grained check by checking intersection between the rectangles' diagonal
			// and the line segments
			var edgeData = GetData<EdgeData>();
			if (edgeData == null)
				return false;

			IConnector outputData = edgeData.output;
			IConnector inputData = edgeData.input;

			if (outputData == null && inputData == null)
				return false;

			Vector2 from = Vector2.zero;
			Vector2 to = Vector2.zero;
			GetFromToPoints(ref from, ref to);

			Orientation orientation = outputData != null ? outputData.orientation : inputData.orientation;

			Vector3[] points, tangents;

			GetTangents(orientation, from, to, out points, out tangents);
			Vector3[] allPoints = Handles.MakeBezierPoints(points[0], points[1], tangents[0], tangents[1], 20);

			for (int a = 0; a < allPoints.Length; a++)
			{
				if (a >= allPoints.Length - 1)
				{
					break;
				}

				var segmentA = new Vector2(allPoints[a].x, allPoints[a].y);
				var segmentB = new Vector2(allPoints[a + 1].x, allPoints[a + 1].y);

				if (RectUtils.IntersectsSegment(rect, segmentA, segmentB))
					return true;
			}

			return false;
		}

		public override bool ContainsPoint(Vector2 localPoint)
		{
			// bounding box check succeeded, do more fine grained check by measuring distance to bezier points
			var edgeData = GetData<EdgeData>();
			if (edgeData == null)
				return false;

			IConnector outputData = edgeData.output;
			IConnector inputData = edgeData.input;

			if (outputData == null && inputData == null)
				return false;

			Vector2 from = Vector2.zero;
			Vector2 to = Vector2.zero;
			GetFromToPoints(ref from, ref to);

			// exclude endpoints
			if (Vector2.Distance(from, localPoint) <= 2*k_EndPointRadius ||
				Vector2.Distance(to, localPoint) <= 2*k_EndPointRadius)
			{
				return false;
			}

			Orientation orientation = outputData != null ? outputData.orientation : inputData.orientation;

			Vector3[] points, tangents;
			GetTangents(orientation, from, to, out points, out tangents);
			Vector3[] allPoints = Handles.MakeBezierPoints(points[0], points[1], tangents[0], tangents[1], 20);

			float minDistance = Mathf.Infinity;
			foreach (Vector3 currentPoint in allPoints)
			{
				float distance = Vector3.Distance(currentPoint, localPoint);
				minDistance = Mathf.Min(minDistance, distance);
				if (minDistance < k_InterceptWidth)
				{
					return true;
				}
			}

			return false;
		}

		public override void DoRepaint(IStylePainter painter)
		{
			base.DoRepaint(painter);
			DrawEdge(painter);
		}

		protected void GetFromToPoints(ref Vector2 from, ref Vector2 to)
		{
			var edgeData = GetData<EdgeData>();
			if (edgeData == null)
				return;

			IConnector outputData = edgeData.output;
			IConnector inputData = edgeData.input;
			if (outputData == null && inputData == null)
				return;

			if (outputData != null)
			{
				GraphElement leftAnchor = parent.allElements.OfType<GraphElement>().First(e => e.dataProvider as IConnector == outputData);
				if (leftAnchor != null)
				{
					from = leftAnchor.GetGlobalCenter();
					from = globalTransform.inverse.MultiplyPoint3x4(from);
				}
			}
			else if (edgeData.candidate)
			{
				from = globalTransform.inverse.MultiplyPoint3x4(new Vector3(edgeData.candidatePosition.x, edgeData.candidatePosition.y));
			}

			if (inputData != null)
			{
				GraphElement rightAnchor = parent.allElements.OfType<GraphElement>().First(e => e.dataProvider as IConnector == inputData);
				if (rightAnchor != null)
				{
					to = rightAnchor.GetGlobalCenter();
					to = globalTransform.inverse.MultiplyPoint3x4(to);
				}
			}
			else if (edgeData.candidate)
			{
				to = globalTransform.inverse.MultiplyPoint3x4(new Vector3(edgeData.candidatePosition.x, edgeData.candidatePosition.y));
			}
		}

		protected virtual void DrawEdge(IStylePainter painter)
		{
			var edgeData = GetData<EdgeData>();
			if (edgeData == null)
				return;

			IConnector outputData = edgeData.output;
			IConnector inputData = edgeData.input;

			if (outputData == null && inputData == null)
				return;

			Vector2 from = Vector2.zero;
			Vector2 to = Vector2.zero;
			GetFromToPoints(ref from, ref to);

			Color edgeColor = (GetData<EdgeData>() != null && GetData<EdgeData>().selected) ? Color.yellow : Color.white;

			Orientation orientation = outputData != null ? outputData.orientation : inputData.orientation;

			Vector3[] points, tangents;
			GetTangents(orientation, from, to, out points, out tangents);
			Handles.DrawBezier(points[0], points[1], tangents[0], tangents[1], edgeColor, null, 5f);

			// little widget on the middle of the edge
			Vector3[] allPoints = Handles.MakeBezierPoints(points[0], points[1], tangents[0], tangents[1], 20);
			Color oldColor = Handles.color;
			Handles.color = Color.blue;
			Handles.DrawSolidDisc(allPoints[10], new Vector3(0.0f, 0.0f, -1.0f), 6f);
			Handles.color = edgeColor;
			Handles.DrawWireDisc(allPoints[10], new Vector3(0.0f, 0.0f, -1.0f), 6f);
			Handles.DrawWireDisc(allPoints[10], new Vector3(0.0f, 0.0f, -1.0f), 5f);

			// TODO need to fix color of unconnected ends now that we've changed how the connection being built work (i.e. left is not always guaranteed to be the connected end... in fact, left doesn't exist anymore)
			// dot on top of anchor showing it's connected
			Handles.color = new Color(0.3f, 0.4f, 1.0f, 1.0f);
			Handles.DrawSolidDisc(from, new Vector3(0.0f, 0.0f, -1.0f), k_EndPointRadius);
			if (edgeData.input == null)
				Handles.color = oldColor;
			Handles.DrawSolidDisc(to, new Vector3(0.0f, 0.0f, -1.0f), k_EndPointRadius);
			Handles.color = oldColor;
		}
	}
}
