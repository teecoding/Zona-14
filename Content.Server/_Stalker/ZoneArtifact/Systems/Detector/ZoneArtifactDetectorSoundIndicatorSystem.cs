using Content.Server._Stalker.ZoneArtifact.Components.Detector;
using Robust.Server.Audio;
using Robust.Shared.Timing;
using ZoneArtifactDetectorComponent = Content.Shared._Stalker.ZoneArtifact.Components.ZoneArtifactDetectorComponent;

namespace Content.Server._Stalker.ZoneArtifact.Systems.Detector;

public sealed class ZoneArtifactDetectorSoundIndicatorSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly ZoneArtifactDetectorSystem _artifactDetector = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ZoneArtifactDetectorSoundIndicatorComponent, ZoneArtifactDetectorComponent>();
        while (query.MoveNext(out var uid, out var indicator, out var detector))
        {
            if (!_artifactDetector.Enabled((uid, detector)))
                continue;

            if (_timing.CurTime < indicator.NextTime)
                continue;

            if (detector.ClosestEntity is null || detector.ClosestDistance is not { } distance)
                continue;

            _audio.PlayPvs(indicator.Sound, uid);

            var scalingFactor = distance / detector.DetectionDistance;
            var interval = (indicator.MaxInterval - indicator.MinInterval) * scalingFactor + indicator.MinInterval;


                indicator.NextTime = _timing.CurTime + interval;
        }
    }
}
