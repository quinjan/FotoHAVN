using FotoHavn.Core.Camera;
using FotoHavn.Core.Camera.Policies;
using FotoHavn.Protocol.Cameras;

namespace FotoHavn.Core.Tests.Cameras.Policies;

public sealed class PreviewBackPressureTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset At(double milliseconds) => T0.AddMilliseconds(milliseconds);

    [Fact]
    public void A_new_channel_has_nothing_in_flight_and_empty_stats()
    {
        var pressure = new PreviewBackPressure();

        Assert.False(pressure.IsInFlight(T0));
        Assert.Equal(new PreviewFrameStats(0, 0, null, 0), pressure.Snapshot(T0));
    }

    [Fact]
    public void The_first_frame_claims_the_slot()
    {
        var pressure = new PreviewBackPressure();

        Assert.True(pressure.TryBegin(1, T0));
        Assert.True(pressure.IsInFlight(T0));
    }

    [Fact]
    public void While_a_frame_is_in_flight_the_next_is_dropped_and_counted()
    {
        var pressure = new PreviewBackPressure();
        pressure.TryBegin(1, T0);

        Assert.False(pressure.TryBegin(2, At(33)));
        Assert.False(pressure.TryBegin(3, At(66)));

        var stats = pressure.Snapshot(At(66));
        Assert.Equal(2, stats.Dropped);
        Assert.Equal(0, stats.Delivered);
        Assert.True(pressure.IsInFlight(At(66)));
    }

    [Fact]
    public void An_ack_of_the_in_flight_frame_frees_the_slot_and_counts_it_delivered()
    {
        var pressure = new PreviewBackPressure();
        pressure.TryBegin(7, T0);

        Assert.True(pressure.Acknowledge(7, At(20)));

        Assert.False(pressure.IsInFlight(At(20)));
        var stats = pressure.Snapshot(At(20));
        Assert.Equal(1, stats.Delivered);
        Assert.Equal(At(20), stats.LastDeliveredAt);
        Assert.True(pressure.TryBegin(8, At(21)));
    }

    [Fact]
    public void A_duplicate_ack_changes_nothing()
    {
        var pressure = new PreviewBackPressure();
        pressure.TryBegin(7, T0);
        pressure.Acknowledge(7, At(20));

        Assert.False(pressure.Acknowledge(7, At(25)));

        var stats = pressure.Snapshot(At(25));
        Assert.Equal(1, stats.Delivered);
        Assert.Equal(At(20), stats.LastDeliveredAt);
    }

    [Fact]
    public void An_ack_with_nothing_in_flight_is_ignored()
    {
        var pressure = new PreviewBackPressure();

        Assert.False(pressure.Acknowledge(0, T0));
        Assert.Equal(0, pressure.Snapshot(T0).Delivered);
    }

    [Fact]
    public void A_stale_ack_for_another_sequence_is_ignored_and_the_slot_stays_taken()
    {
        var pressure = new PreviewBackPressure();
        pressure.TryBegin(10, T0);

        Assert.False(pressure.Acknowledge(9, At(10)));
        Assert.False(pressure.Acknowledge(11, At(10)));

        Assert.True(pressure.IsInFlight(At(10)));
        Assert.Equal(0, pressure.Snapshot(At(10)).Delivered);
        Assert.True(pressure.Acknowledge(10, At(11)));
    }

    [Fact]
    public void An_ack_of_a_frame_that_was_replaced_after_a_timeout_is_stale()
    {
        var pressure = new PreviewBackPressure();
        pressure.TryBegin(1, T0);
        Assert.True(pressure.TryBegin(2, At(1500)));

        Assert.False(pressure.Acknowledge(1, At(1510)));
        Assert.True(pressure.Acknowledge(2, At(1520)));
    }

    [Fact]
    public void A_frame_unacked_for_the_timeout_stops_blocking_and_is_replaced_as_a_drop()
    {
        var pressure = new PreviewBackPressure();
        pressure.TryBegin(1, T0);

        Assert.False(pressure.TryBegin(2, At(999)));
        Assert.True(pressure.IsInFlight(At(999)));
        Assert.False(pressure.IsInFlight(At(1000)));
        Assert.True(pressure.TryBegin(3, At(1000)));

        var stats = pressure.Snapshot(At(1000));
        Assert.Equal(2, stats.Dropped);
        Assert.True(pressure.IsInFlight(At(1001)));
    }

    [Fact]
    public void A_late_ack_of_a_timed_out_frame_that_nobody_replaced_still_counts()
    {
        var pressure = new PreviewBackPressure();
        pressure.TryBegin(1, T0);

        Assert.True(pressure.Acknowledge(1, At(1800)));
        Assert.Equal(1, pressure.Snapshot(At(1800)).Delivered);
        Assert.False(pressure.IsInFlight(At(1800)));
    }

    [Fact]
    public void Reset_frees_the_slot_and_keeps_the_counters()
    {
        var pressure = new PreviewBackPressure();
        pressure.TryBegin(1, T0);
        pressure.Acknowledge(1, At(10));
        pressure.TryBegin(2, At(20));
        pressure.TryBegin(3, At(30));

        pressure.Reset();

        Assert.False(pressure.IsInFlight(At(31)));
        var stats = pressure.Snapshot(At(31));
        Assert.Equal(1, stats.Delivered);
        Assert.Equal(1, stats.Dropped);
        Assert.Equal(At(10), stats.LastDeliveredAt);
        Assert.False(pressure.Acknowledge(2, At(32)));
        Assert.True(pressure.TryBegin(4, At(33)));
    }

    [Fact]
    public void Resetting_an_idle_channel_is_harmless()
    {
        var pressure = new PreviewBackPressure();

        pressure.Reset();

        Assert.True(pressure.TryBegin(1, T0));
    }

    [Fact]
    public void Sequences_compare_on_the_wire_s_thirty_two_bits()
    {
        var pressure = new PreviewBackPressure();
        var sequence = (1L << 32) + 5;
        pressure.TryBegin(sequence, T0);

        Assert.Equal(5u, PreviewFrameHeader.ToWireSequence(sequence));
        Assert.False(pressure.Acknowledge(6, At(1)));
        Assert.True(pressure.Acknowledge(5, At(2)));
    }

    [Fact]
    public void The_largest_wire_sequence_round_trips()
    {
        var pressure = new PreviewBackPressure();
        pressure.TryBegin(uint.MaxValue, T0);

        Assert.True(pressure.Acknowledge(uint.MaxValue, At(1)));
    }

    [Fact]
    public void A_clock_that_steps_back_does_not_time_the_frame_out()
    {
        var pressure = new PreviewBackPressure();
        pressure.TryBegin(1, At(5000));

        Assert.True(pressure.IsInFlight(At(0)));
        Assert.False(pressure.TryBegin(2, At(0)));
    }

    [Fact]
    public void The_rate_is_the_deliveries_in_the_last_two_seconds_per_second()
    {
        var pressure = new PreviewBackPressure();
        for (var i = 0; i < 30; i++)
        {
            var at = At(i * 66);
            pressure.TryBegin(i + 1, at);
            pressure.Acknowledge((uint)(i + 1), at);
        }

        var stats = pressure.Snapshot(At(30 * 66 - 66 + 1));

        Assert.Equal(30, stats.Delivered);
        Assert.Equal(15d, stats.DeliveredPerSecond, 6);
    }

    [Fact]
    public void Deliveries_leave_the_window_exactly_two_seconds_later()
    {
        var pressure = new PreviewBackPressure();
        pressure.TryBegin(1, T0);
        pressure.Acknowledge(1, T0);

        Assert.Equal(0.5, pressure.Snapshot(At(1999)).DeliveredPerSecond, 6);
        Assert.Equal(0, pressure.Snapshot(At(2000)).DeliveredPerSecond);
        Assert.Equal(1, pressure.Snapshot(At(2000)).Delivered);
    }

    [Fact]
    public void A_snapshot_before_a_delivery_does_not_count_it_and_snapshots_do_not_consume_the_window()
    {
        var pressure = new PreviewBackPressure();
        pressure.TryBegin(1, At(1000));
        pressure.Acknowledge(1, At(1000));

        Assert.Equal(0, pressure.Snapshot(At(500)).DeliveredPerSecond);
        Assert.Equal(0.5, pressure.Snapshot(At(1500)).DeliveredPerSecond, 6);
        Assert.Equal(0.5, pressure.Snapshot(At(1500)).DeliveredPerSecond, 6);
        Assert.Equal(0, pressure.Snapshot(At(3000)).DeliveredPerSecond);
        Assert.Equal(0.5, pressure.Snapshot(At(2999)).DeliveredPerSecond, 6);
    }

    [Fact]
    public void Old_deliveries_are_pruned_as_new_acks_arrive_without_losing_the_totals()
    {
        var pressure = new PreviewBackPressure();
        for (var i = 0; i < 500; i++)
        {
            var at = At(i * 100);
            pressure.TryBegin(i, at);
            pressure.Acknowledge((uint)i, at);
        }

        var stats = pressure.Snapshot(At(499 * 100));
        Assert.Equal(500, stats.Delivered);
        Assert.Equal(10d, stats.DeliveredPerSecond, 6);
    }

    [Fact]
    public async Task Concurrent_posts_and_acks_keep_the_counters_consistent()
    {
        const int Frames = 20_000;
        var pressure = new PreviewBackPressure();
        var begun = 0L;
        var acked = 0L;
        var latestBegun = 0L;

        var poster = Task.Run(() =>
        {
            for (var sequence = 1L; sequence <= Frames; sequence++)
            {
                if (pressure.TryBegin(sequence, T0))
                {
                    Interlocked.Increment(ref begun);
                    Interlocked.Exchange(ref latestBegun, sequence);
                }
            }
        }, TestContext.Current.CancellationToken);
        var acker = Task.Run(() =>
        {
            for (var i = 0; i < Frames; i++)
            {
                if (pressure.Acknowledge((uint)Interlocked.Read(ref latestBegun), T0))
                {
                    Interlocked.Increment(ref acked);
                }
            }
        }, TestContext.Current.CancellationToken);

        await Task.WhenAll(poster, acker);

        var stats = pressure.Snapshot(T0);
        Assert.Equal(Interlocked.Read(ref acked), stats.Delivered);
        Assert.Equal(Frames, Interlocked.Read(ref begun) + stats.Dropped);
        Assert.True(stats.Delivered <= Interlocked.Read(ref begun));
    }
}

public sealed class MirrorTransformTests
{
    private static byte[] Pixels(int width, int height, int stride, byte padding = 0xEE)
    {
        var buffer = new byte[stride * (height - 1) + (width * 4)];
        Array.Fill(buffer, padding);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = (y * stride) + (x * 4);
                buffer[offset] = (byte)x;
                buffer[offset + 1] = (byte)y;
                buffer[offset + 2] = (byte)(x ^ y);
                buffer[offset + 3] = 255;
            }
        }

        return buffer;
    }

    [Theory]
    [InlineData(4, 3, 16)]
    [InlineData(5, 2, 20)]
    [InlineData(7, 4, 28)]
    [InlineData(2, 2, 8)]
    [InlineData(3, 3, 24)]
    [InlineData(5, 3, 32)]
    [InlineData(1, 5, 4)]
    [InlineData(1, 5, 12)]
    [InlineData(1, 1, 4)]
    public void Every_pixel_moves_to_the_mirrored_column_and_padding_is_untouched(int width, int height, int stride)
    {
        var original = Pixels(width, height, stride);
        var mirrored = (byte[])original.Clone();

        MirrorTransform.MirrorBgraInPlace(mirrored, width, height, stride);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var from = (y * stride) + ((width - 1 - x) * 4);
                var to = (y * stride) + (x * 4);
                Assert.Equal(original.AsSpan(from, 4).ToArray(), mirrored.AsSpan(to, 4).ToArray());
            }

            for (var i = width * 4; i < stride && (y * stride) + i < original.Length; i++)
            {
                Assert.Equal(0xEE, mirrored[(y * stride) + i]);
            }
        }
    }

    [Theory]
    [InlineData(1280, 720, 5120)]
    [InlineData(6, 3, 24)]
    [InlineData(5, 3, 32)]
    [InlineData(1, 4, 8)]
    public void Mirroring_twice_restores_the_original(int width, int height, int stride)
    {
        var original = Pixels(width, height, stride);
        var buffer = (byte[])original.Clone();

        MirrorTransform.MirrorBgraInPlace(buffer, width, height, stride);
        MirrorTransform.MirrorBgraInPlace(buffer, width, height, stride);

        Assert.Equal(original, buffer);
    }

    [Fact]
    public void A_two_pixel_row_swaps_its_pixels()
    {
        byte[] buffer = [1, 2, 3, 4, 5, 6, 7, 8];

        MirrorTransform.MirrorBgraInPlace(buffer, 2, 1, 8);

        Assert.Equal(new byte[] { 5, 6, 7, 8, 1, 2, 3, 4 }, buffer);
    }

    [Fact]
    public void An_odd_width_keeps_the_middle_pixel_in_place()
    {
        byte[] buffer = [1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3];

        MirrorTransform.MirrorBgraInPlace(buffer, 3, 1, 12);

        Assert.Equal(new byte[] { 3, 3, 3, 3, 2, 2, 2, 2, 1, 1, 1, 1 }, buffer);
    }

    [Fact]
    public void A_one_pixel_wide_image_is_unchanged()
    {
        byte[] buffer = [1, 2, 3, 4, 0xEE, 0xEE, 0xEE, 0xEE, 5, 6, 7, 8];
        var before = (byte[])buffer.Clone();

        MirrorTransform.MirrorBgraInPlace(buffer, 1, 2, 8);

        Assert.Equal(before, buffer);
    }

    [Fact]
    public void The_last_row_needs_no_trailing_padding()
    {
        var buffer = Pixels(3, 2, 16);
        Assert.Equal((16 * 1) + 12, buffer.Length);

        MirrorTransform.MirrorBgraInPlace(buffer, 3, 2, 16);

        Assert.Equal(2, buffer[(1 * 16) + 0]);
    }

    [Fact]
    public void A_buffer_longer_than_the_frame_leaves_the_excess_alone()
    {
        var buffer = new byte[8 + 3];
        buffer[0] = 1;
        buffer[4] = 2;
        buffer[8] = 9;

        MirrorTransform.MirrorBgraInPlace(buffer, 2, 1, 8);

        Assert.Equal(new byte[] { 2, 0, 0, 0, 1, 0, 0, 0, 9, 0, 0 }, buffer);
    }

    [Theory]
    [InlineData(0, 1, 4)]
    [InlineData(-1, 1, 4)]
    [InlineData(1, 0, 4)]
    [InlineData(1, -1, 4)]
    [InlineData(2, 1, 7)]
    [InlineData(2, 1, 0)]
    [InlineData(2, 1, -8)]
    public void A_bad_size_or_stride_is_out_of_range(int width, int height, int stride)
    {
        var buffer = new byte[64];

        Assert.Throws<ArgumentOutOfRangeException>(() => MirrorTransform.MirrorBgraInPlace(buffer, width, height, stride));
    }

    [Theory]
    [InlineData(2, 2, 8, 15)]
    [InlineData(2, 2, 12, 19)]
    [InlineData(3, 1, 12, 11)]
    [InlineData(1, 1, 4, 0)]
    public void A_buffer_shorter_than_the_frame_is_rejected(int width, int height, int stride, int length)
    {
        var buffer = new byte[length];

        Assert.Throws<ArgumentException>(() => MirrorTransform.MirrorBgraInPlace(buffer, width, height, stride));
    }

    [Fact]
    public void Huge_dimensions_are_rejected_without_overflowing()
    {
        var buffer = new byte[64];

        Assert.Throws<ArgumentException>(() => MirrorTransform.MirrorBgraInPlace(buffer, 20_000, 20_000, 80_000));
        Assert.Throws<ArgumentOutOfRangeException>(() => MirrorTransform.MirrorBgraInPlace(buffer, int.MaxValue / 2, 2, int.MaxValue));
    }
}
