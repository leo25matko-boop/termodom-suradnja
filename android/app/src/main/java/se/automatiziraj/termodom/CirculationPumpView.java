package se.automatiziraj.termodom;

import android.animation.ValueAnimator;
import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Path;
import android.os.SystemClock;
import android.view.View;

import java.time.Instant;

/** Status symbol: the arrow rotates only while the running reading is current. */
final class CirculationPumpView extends View {
    private static final int ON = 0xff76ddac, OFF = 0xffff9d9b, MUTED = 0xffa1b6c1;
    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Path tip = new Path();
    private final HouseTelemetry telemetry;
    private final int index;

    CirculationPumpView(Context context, HouseTelemetry telemetry, int index) {
        super(context);
        this.telemetry = telemetry;
        this.index = index;
        setImportantForAccessibility(IMPORTANT_FOR_ACCESSIBILITY_NO);
    }

    @Override
    protected void onDraw(Canvas canvas) {
        super.onDraw(canvas);
        Instant now = Instant.now();
        HouseTelemetry.Pump state = telemetry.pumpState(index, now);
        boolean fresh = telemetry.pumpReadings[index].age(now) == HouseTelemetry.Age.FRESH;
        boolean animate = fresh && state == HouseTelemetry.Pump.ON && ValueAnimator.areAnimatorsEnabled();
        float scale = Math.min(getWidth(), getHeight()) / 32f;
        int save = canvas.save();
        canvas.translate(getWidth() / 2f, getHeight() / 2f);
        canvas.scale(scale, scale);
        paint.setStyle(Paint.Style.FILL);
        paint.setColor(0xff36515a);
        canvas.drawRoundRect(-14, -6, 14, 6, 2, 2, paint);
        paint.setColor(0xff263e48);
        canvas.drawCircle(0, 0, 11, paint);
        paint.setStyle(Paint.Style.STROKE);
        paint.setStrokeWidth(1);
        paint.setColor(0xff6c8791);
        canvas.drawCircle(0, 0, 10.5f, paint);
        paint.setColor(!fresh ? MUTED : state == HouseTelemetry.Pump.ON ? ON : OFF);
        paint.setStrokeCap(Paint.Cap.ROUND);
        paint.setStrokeWidth(1.7f);
        if (state == HouseTelemetry.Pump.UNKNOWN) {
            canvas.drawLine(-3, 0, 3, 0, paint);
        } else {
            if (animate) canvas.rotate((SystemClock.uptimeMillis() % 1700) * 360f / 1700);
            canvas.drawArc(-6, -6, 6, 6, 20, 275, false, paint);
            tip.reset();
            tip.moveTo(0, -7.6f);
            tip.lineTo(3.7f, -5.4f);
            tip.lineTo(0, -2.9f);
            canvas.drawPath(tip, paint);
        }
        canvas.restoreToCount(save);
        if (animate && isShown() && getWindowVisibility() == VISIBLE) postInvalidateDelayed(42);
    }
}
