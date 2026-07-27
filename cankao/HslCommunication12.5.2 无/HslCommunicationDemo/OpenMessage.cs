using System.Collections.Generic;

namespace HslCommunicationDemo
{
	public class OpenMessage
	{
		public int MID { get; set; }

		public int Revision { get; set; }

		public int StationID { get; set; }

		public int SpindleID { get; set; }

		public List<string> DataField { get; set; }

		public OpenMessage(int mid, int revision, int stationID, int spindleID, List<string> dataField)
		{
			MID = mid;
			Revision = revision;
			StationID = stationID;
			SpindleID = spindleID;
			DataField = dataField;
		}
	}
}
