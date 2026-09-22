using NextSekai;
using Sekai;

public class ConvUtil
{
  public static string[] EventDataArchetypes =
  [
    "#BPM_CHANGE",
    "#TIMESCALE_CHANGE",
  ];

  public static string[] NoteArchetypes =
  [
    "NormalTapNote",
    "CriticalTapNote",
    "NormalFlickNote",
    "CriticalFlickNote",
    "NormalTraceNote",
    "CriticalTraceNote",
    "NormalTraceFlickNote",
    "CriticalTraceFlickNote",
  ];

  public static string[] LongHeadArchetypes =
  [
    "NormalHeadTapNote",
    "CriticalHeadTapNote",
    "NormalHeadFlickNote",
    "CriticalHeadFlickNote",
    "NormalHeadTraceNote",
    "CriticalHeadTraceNote",
    "NormalHeadTraceFlickNote",
    "CriticalHeadTraceFlickNote",
    "NormalHeadReleaseNote",
    "CriticalHeadReleaseNote",
  ];

  public static string[] LongTailArchetypes =
  [
    "NormalTailTapNote",
    "CriticalTailTapNote",
    "NormalTailFlickNote",
    "CriticalTailFlickNote",
    "NormalTailTraceNote",
    "CriticalTailTraceNote",
    "NormalTailTraceFlickNote",
    "CriticalTailTraceFlickNote",
    "NormalTailReleaseNote",
    "CriticalTailReleaseNote",
  ];

  public static string[] ConnectionArchetypes =
  [
    "NormalTickNote",
    "CriticalTickNote",
    "TransientHiddenTickNote",
  ];

  public static string[] FlickHeadArchetypes =
  [
    "NormalHeadFlickNote",
    "CriticalHeadFlickNote",
  ];

  public static long Beat2Ticks(double beat)
  {
    return (long)(480L * beat);
  }

  public static (int, int) UnconvertLane(double lane, double size)
  {
    double laneStart = lane - size + 5.5 + 0.5;
    double laneEnd = lane + size + 5.5 - 1 + 0.5;
    return ((int)laneStart, (int)laneEnd);
  }

  public static (Entity[], Entity[], Entity[]) FilterEntities(Entity[] entities)
  {
    List<Entity> eventDataEntities = new List<Entity>();
    List<Entity> noteEntities = new List<Entity>();
    List<Entity> longEntities = new List<Entity>();

    // unreturned
    List<Entity> usedTimescaleGroups = new List<Entity>();

    // filter unused layers from having hi-speeds changed
    foreach (Entity entity in entities)
    {
      if (entity.archetype != "#TIMESCALE_GROUP")
      {
        continue;
      }

      bool used = entities.Any(checkEntity => NoteArchetypes.Concat(LongHeadArchetypes.Concat(LongTailArchetypes)).Contains(checkEntity.archetype) && 
                               checkEntity.data.FirstOrDefault(d => d.name == "#TIMESCALE_GROUP" && d._ref == entity.name) != null);

      if (used == true)
      {
        usedTimescaleGroups.Add(entity);
      }
    }

    foreach (Entity entity in entities)
    {
      if (EventDataArchetypes.Contains(entity.archetype))
      {
        if (entity.archetype == "#TIMESCALE_CHANGE")
        {
          string layerRef = entity.data.FirstOrDefault(d => d.name == "#TIMESCALE_GROUP")?._ref;
          Entity layerEntity = entities.FirstOrDefault(e => e.name == layerRef);

          if (layerEntity != null && usedTimescaleGroups.Contains(layerEntity))
          {
            eventDataEntities.Add(entity);
          }
        }
        else
        {
          eventDataEntities.Add(entity);
        }
      }
      else if (NoteArchetypes.Contains(entity.archetype))
      {
        noteEntities.Add(entity);
      }
      else if (LongHeadArchetypes.Contains(entity.archetype))
      {
        longEntities.Add(entity);

        Entity[] followedEntities = FollowLongHead(entities, entity.name);
        foreach (Entity followedEntity in followedEntities)
        {
          if (!longEntities.Contains(followedEntity))
          {
            longEntities.Add(followedEntity);
          }
        }
      }
    }

    return (eventDataEntities.ToArray(), noteEntities.ToArray(), longEntities.ToArray());
  }

  public static Entity[] FollowLongHead(Entity[] entities, string targetName)
  {
    List<Entity> outList = new List<Entity>();

    Entity targetEntity = entities.FirstOrDefault(e => e.name == targetName);

    if (targetEntity == null)
    {
      return outList.ToArray();
    }

    Data nextData = targetEntity.data.FirstOrDefault(data => data.name == "next");

    if (nextData != null)
    {
      Entity nextEntity = entities.FirstOrDefault(e => e.name == nextData._ref);
      if (nextEntity != null)
      {
        outList.Add(nextEntity);

        Entity[] nexterEntities = FollowLongHead(entities, nextEntity.name);

        foreach (Entity nexterEntity in nexterEntities)
        {
          if (!outList.Contains(nexterEntity))
          {
            outList.Add(nexterEntity);
          }
        }
      }
    }

    return outList.ToArray();
  }

  // walk Connector links to find hold/guide chains
  public static List<Entity[]> BuildChains(Entity[] entities)
  {
    Dictionary<string, Entity> byName = entities.Where(e => e.name != null).ToDictionary(e => e.name, e => e);

    List<(string headName, string tailName)> links = new List<(string, string)>();
    foreach (Entity entity in entities)
    {
      if (entity.archetype != "Connector") continue;
      Data head = entity.data.FirstOrDefault(d => d.name == "head");
      Data tail = entity.data.FirstOrDefault(d => d.name == "tail");
      if (head != null && tail != null)
      {
        links.Add((head._ref, tail._ref));
      }
    }

    Dictionary<string, string> nextOf = new Dictionary<string, string>();
    HashSet<string> hasIncoming = new HashSet<string>();
    foreach ((string headName, string tailName) in links)
    {
      nextOf[headName] = tailName;
      hasIncoming.Add(tailName);
    }

    List<string> chainHeadNames = nextOf.Keys.Where(n => !hasIncoming.Contains(n)).ToList();

    List<Entity[]> chains = new List<Entity[]>();
    HashSet<string> visited = new HashSet<string>();
    foreach (string startName in chainHeadNames)
    {
      if (visited.Contains(startName)) continue;

      List<Entity> chain = new List<Entity>();
      string cur = startName;
      while (cur != null && !visited.Contains(cur))
      {
        if (!byName.TryGetValue(cur, out Entity entity)) break;
        chain.Add(entity);
        visited.Add(cur);
        nextOf.TryGetValue(cur, out cur);
      }

      if (chain.Count >= 2) chains.Add(chain.ToArray());
    }

    return chains;
  }

  // green or yellow guide, other than that would turned into green, null if not a guide
  public static int? GetGuideKind(Entity[] chain)
  {
    foreach (Entity entity in chain)
    {
      Data segmentKindData = entity.data.FirstOrDefault(d => d.name == "segmentKind");
      if (segmentKindData != null)
      {
        int kind = (int)segmentKindData.value;
        if (kind >= 101 && kind <= 108)
        {
          return kind == 105 ? 105 : 103;
        }
      }
    }
    return null;
  }

  public static (Note[] notes, Note[] extraNotes) ProcessChain(Entity[] chain, ref int idCounter)
  {
    int? guideKind = GetGuideKind(chain);
    bool isGuide = guideKind.HasValue;
    NoteType guideType = guideKind == 105 ? NoteType.Critical : NoteType.Default;

    // anchors don't have Critical/Normal in the name, so grab it off whatever entity does
    NoteType holdType = NoteType.Default;
    if (!isGuide)
    {
      foreach (Entity e in chain)
      {
        if (e.archetype.Contains("Critical")) { holdType = NoteType.Critical; break; }
        if (e.archetype.Contains("Normal")) { holdType = NoteType.Default; break; }
      }
    }

    List<Note> notes = new List<Note>();
    List<Note> extraNotes = new List<Note>();

    for (int i = 0; i < chain.Length; i++)
    {
      Entity entity = chain[i];
      bool isFirst = i == 0;
      bool isLast = i == chain.Length - 1;

      long ticks = Beat2Ticks(entity.data.FirstOrDefault(d => d.name == "#BEAT").value);
      (int, int) lanes = UnconvertLane(entity.data.FirstOrDefault(d => d.name == "lane").value,
        entity.data.FirstOrDefault(d => d.name == "size").value);
      NoteType type = isGuide ? guideType : holdType;
      Data easeData = entity.data.FirstOrDefault(d => d.name == "connectorEase");
      NoteLineType noteLineType = easeData != null ? GetNoteLineType((int)easeData.value) : NoteLineType.Linear;

      NoteCategory category;
      if (isGuide)
      {
        category = isFirst ? NoteCategory.Guide : isLast ? NoteCategory.GuideEnd : NoteCategory.GuideHidden;
      }
      else if (isFirst)
      {
        category = NoteCategory.FrictionHideLong;
      }
      else if (isLast)
      {
        category = NoteCategory.Long;
      }
      else
      {
        category = NoteCategory.Hidden;
      }

      NoteBaseType noteBaseType = GetNoteBaseType(category, isFirst, isLast, false);
      int id = idCounter++;
      Note note = new Note(id, ticks, lanes.Item1, lanes.Item2, category, type, 1.0, noteLineType,
        noteBaseType, -1, -1, NoteDirection.Default, category == NoteCategory.Skip);
      notes.Add(note);

      // hold notes with flick head: add a separate flick note on top of the head
      if (!isGuide && isFirst && FlickHeadArchetypes.Contains(entity.archetype))
      {
        Data dirData = entity.data.FirstOrDefault(d => d.name == "direction");
        NoteDirection direction = dirData != null ? GetNoteDirection((int)dirData.value) : NoteDirection.Default;
        int flickId = idCounter++;
        extraNotes.Add(new Note(flickId, ticks, lanes.Item1, lanes.Item2, NoteCategory.Flick, type, 1.0,
          NoteLineType.Linear, NoteBaseType.Flick, -1, -1, direction, false));
      }
    }

    for (int i = 0; i < notes.Count - 1; i++)
    {
      notes[i].nextConnectionId = notes[i + 1].id;
      notes[i + 1].previousConnectionId = notes[i].id;
    }

    return (notes.ToArray(), extraNotes.ToArray());
  }

  public static MusicScoreEventData ProcessEventData(Entity entity, int id)
  {
    if (EventDataArchetypes.Contains(entity.archetype))
    {
      MusicScoreEventType eventType = GetEventType(entity.archetype);
      long ticks = Beat2Ticks(entity.data.FirstOrDefault(data => data.name == "#BEAT").value);
      object changeValue = eventType switch
      {
        MusicScoreEventType.BPM => entity.data.FirstOrDefault(data => data.name == "#BPM").value,
        MusicScoreEventType.HighSpeed => entity.data.First(data => data.name == "#TIMESCALE").value,
        _ => null,
      };

      return new MusicScoreEventData(
        id,
        eventType,
        ticks,
        changeValue
      );
    }

    throw new InvalidOperationException($"Attempted to run ProcessEventData on a non-event-data entity. Archetype: {entity.archetype}.");
  }

  public static MusicScoreEventType GetEventType(string archetype)
  {
    if (archetype == "#BPM_CHANGE")
    {
      return MusicScoreEventType.BPM;
    }
    else if (archetype == "#TIMESCALE_CHANGE")
    {
      return MusicScoreEventType.HighSpeed;
    }

    throw new ArgumentException($"Unable to process archetype {archetype} to MusicScoreEventType.");
  }

  public static Note ProcessNote(Entity entity, int id)
  {
    if (NoteArchetypes.Contains(entity.archetype) || LongHeadArchetypes.Contains(entity.archetype) ||
        LongTailArchetypes.Contains(entity.archetype) || ConnectionArchetypes.Contains(entity.archetype) ||
        entity.archetype == "AnchorNote")
    {
      long ticks = Beat2Ticks(entity.data.FirstOrDefault(data => data.name == "#BEAT").value);
      (int, int) lanes = UnconvertLane(entity.data.FirstOrDefault(data => data.name == "lane").value,
        entity.data.FirstOrDefault(data => data.name == "size").value);
      NoteCategory category = GetNoteCategory(entity);
      NoteType type = entity.archetype.Contains("Critical") ? NoteType.Critical : NoteType.Default;
      NoteLineType noteLineType = GetNoteLineType((int)entity.data.FirstOrDefault(data => data.name == "connectorEase").value);
      NoteBaseType noteBaseType = GetNoteBaseType(category, false, false, true);
      NoteDirection direction = GetNoteDirection((int)entity.data.FirstOrDefault(data => data.name == "direction").value);
      Data isAttachedData = entity.data.FirstOrDefault(data => data.name == "isAttached");
      bool isAttached = isAttachedData != null && (int)isAttachedData.value == 1;
      bool isSkip = category == NoteCategory.Skip || (category == NoteCategory.Connection && isAttached);

      return new Note(
        id,
        ticks,
        lanes.Item1,
        lanes.Item2,
        category,
        type,
        1.0,
        noteLineType,
        noteBaseType,
        -1, // long notes will implement manually in Program.cs
        -1, // long notes will implement manually in Program.cs
        direction,
        isSkip
      );
    }

    throw new InvalidOperationException($"Attempted to run ProcessNote on a non-note entity. Archetype: {entity.archetype}.");
  }

  public static NoteCategory GetNoteCategory(Entity entity)
  {
    string archetype = entity.archetype;

    if (archetype.EndsWith("alTapNote"))
    {
      return NoteCategory.Normal;
    }
    else if (archetype.EndsWith("alFlickNote") || archetype.EndsWith("alTailFlickNote"))
    {
      return NoteCategory.Flick;
    }
    else if (archetype.EndsWith("alTraceNote") || archetype.EndsWith("alTailTraceNote"))
    {
      return NoteCategory.Friction;
    }
    else if (archetype.EndsWith("alTraceFlickNote") || archetype.EndsWith("alTailTraceFlickNote"))
    {
      return NoteCategory.FrictionFlick;
    }
    else if (archetype.EndsWith("alHeadTapNote") || archetype.EndsWith("alHeadFlickNote") ||
             archetype.EndsWith("alHeadReleaseNote") || archetype.EndsWith("alTailTapNote") ||
             archetype.EndsWith("alTailReleaseNote"))
    {
      return NoteCategory.Long;
    }
    else if (archetype.EndsWith("alHeadTraceNote") || archetype.EndsWith("alHeadTraceFlickNote"))
    {
      return NoteCategory.FrictionLong;
    }
    else if (archetype.EndsWith("alTickNote"))
    {
      return NoteCategory.Connection;
    }
    else if (archetype == "TransientHiddenTickNote")
    {
      return NoteCategory.Hidden;
    }
    else if (archetype == "AnchorNote")
    {
      return entity.data.FirstOrDefault(d => d.name == "next") != null ? NoteCategory.Hidden : NoteCategory.FrictionHideLong;
    }

    throw new ArgumentException($"Unable to process archetype {archetype} to NoteCategory.");
  }

  public static NoteLineType GetNoteLineType(int connectorEase)
  {
    return connectorEase switch
    {
      2 => NoteLineType.EaseIn,  // IN_QUAD
      3 => NoteLineType.EaseOut, // OUT_QUAD
      _ => NoteLineType.Linear,  // NONE, LINEAR, IN_OUT_QUAD, OUT_IN_QUAD
    };
  }

  // original: https://github.com/UntitledCharts/sonolus-level-converters/blob/main/sonolus_converters/pjsk/exporter.py#L41
  public static NoteBaseType GetNoteBaseType(NoteCategory category, bool isConnectedFirst, bool isConnectedLast, bool isSingle)
  {
    if (isSingle)
    {
      return category switch
      {
        NoteCategory.Normal => NoteBaseType.Normal,
        NoteCategory.Flick => NoteBaseType.Flick,
        NoteCategory.Friction => NoteBaseType.Friction,
        NoteCategory.FrictionHide => NoteBaseType.FrictionHide,
        NoteCategory.FrictionFlick => NoteBaseType.FrictionFlick,
        _ => NoteBaseType.Normal,
      };
    }

    if (isConnectedFirst)
    {
      return category switch
      {
        NoteCategory.Long => NoteBaseType.Long,
        NoteCategory.FrictionLong => NoteBaseType.FrictionLong,
        NoteCategory.FrictionHideLong => NoteBaseType.FrictionHideLong,
        NoteCategory.Guide => NoteBaseType.Guide,
        _ => NoteBaseType.Long,
      };
    }

    if (isConnectedLast)
    {
      return category switch
      {
        NoteCategory.Normal or NoteCategory.Long => NoteBaseType.Normal,
        NoteCategory.Flick => NoteBaseType.Flick,
        NoteCategory.Friction => NoteBaseType.Friction,
        NoteCategory.FrictionHide => NoteBaseType.FrictionHide,
        NoteCategory.FrictionFlick => NoteBaseType.FrictionFlick,
        NoteCategory.GuideEnd => NoteBaseType.GuideEnd,
        _ => NoteBaseType.Normal,
      };
    }

    // mid
    return category switch
    {
      NoteCategory.Connection => NoteBaseType.Connection,
      NoteCategory.Hidden => NoteBaseType.HiddenConnection,
      NoteCategory.GuideHidden => NoteBaseType.GuideHiddenConnection,
      _ => NoteBaseType.Connection,
    };
  }

  public static NoteDirection GetNoteDirection(int direction)
  {
    return direction switch
    {
      1 => NoteDirection.Left,    // UP_LEFT
      2 => NoteDirection.Right,   // UP_RIGHT
      4 => NoteDirection.Left,    // DOWN_LEFT
      5 => NoteDirection.Right,   // DOWN_RIGHT
      _ => NoteDirection.Default  // UP_OMNI, DOWN_OMNI, or no direction
    };
  }
}
