using System;
using System.Collections.Generic;


// basic data classes to catch the json from the web frontend
[Serializable]
public class ZoneSnapshotResponseDto {
    public string camera_id;
    public Dictionary<string, ZoneViewDto> zones;
}

[Serializable]
public class ZoneViewDto {
    public Dictionary<string, int> colors;
    public int unknown_spool_count;
    public int other_object_count;
    public string updated_at_utc;
}