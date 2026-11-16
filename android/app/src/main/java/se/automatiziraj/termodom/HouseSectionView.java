package se.automatiziraj.termodom;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.LinearGradient;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.Shader;
import android.graphics.Typeface;
import android.os.SystemClock;
import android.view.MotionEvent;
import android.view.View;

import java.time.Instant;

/** A schematic house cutaway: the equipment and pipe routing are illustrative. */
final class HouseSectionView extends View {
    private static final float W = 360, H = 532, MAX_ZOOM = 3f;
    private static final int INK = 0xffeef5f5, MUTED = 0xffa1b6c1, FRAME = 0xff38505a,
            GREEN = 0xff76ddac, RED = 0xffff9d9b, GOLD = 0xffedbf79, BLUE = 0xffa9d7ef,
            PIPE = 0xff54818a, UNKNOWN = 0xffa1b6c1;
    private static final Typeface BOLD = Typeface.create("sans-serif-medium", Typeface.NORMAL);
    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Path path = new Path();
    private final Shader sky = new LinearGradient(0, 0, 0, 260, 0xff233d50, 0xff111e2a, Shader.TileMode.CLAMP);
    private final Shader tank = new LinearGradient(0, 0, 0, 104, 0xff294751, 0xff1e3545, Shader.TileMode.CLAMP);
    private final HouseTelemetry state;
    static final class Viewport { float zoom = 1f, x, y; }
    private final Viewport viewport;
    private int dragId = MotionEvent.INVALID_POINTER_ID, pinchFirst = MotionEvent.INVALID_POINTER_ID,
            pinchSecond = MotionEvent.INVALID_POINTER_ID;
    private float dragX, dragY, pinchSpan, pinchX, pinchY;

    HouseSectionView(Context context, HouseTelemetry state, Viewport viewport) {
        super(context);
        this.state = state;
        this.viewport = viewport;
        setImportantForAccessibility(IMPORTANT_FOR_ACCESSIBILITY_YES);
        setContentDescription(state.description());
        setTag("house-section");
    }

    @Override protected void onMeasure(int widthSpec, int heightSpec) {
        int available = View.MeasureSpec.getSize(widthSpec);
        int maxWidth = Math.round(540 * getResources().getDisplayMetrics().density);
        int drawing = Math.min(maxWidth, available);
        setMeasuredDimension(available, resolveSize(Math.round(drawing * H / W), heightSpec));
    }

    @Override protected void onDraw(Canvas canvas) {
        super.onDraw(canvas);
        float scale = baseScale() * viewport.zoom;
        if (scale <= 0) return;
        Instant now = Instant.now();
        clampViewport();
        int save = canvas.save();
        canvas.translate((getWidth() - W * scale) / 2 + viewport.x, (getHeight() - H * scale) / 2 + viewport.y);
        canvas.scale(scale, scale);
        paint.setShader(sky);
        canvas.drawRoundRect(0, 0, W, H, 19, 19, paint);
        paint.setShader(null);

        // Horizon and grounded structure.
        fill(canvas, 11, 443, 349, 511, 10, 0xff132b33);
        stroke(canvas, 14, 494, 346, 494, 1, 0xff36575a);
        for (int i = 0; i < 14; i++) {
            int x = 16 + i * 25;
            stroke(canvas, x, 498, x + 17, 498, 1, 0xff2c4851);
        }
        fill(canvas, 18, 146, 181, 490, 4, 0xff2f4f58);
        fill(canvas, 23, 151, 176, 483, 3, 0xff1b303c);
        fill(canvas, 24, 158, 175, 311, 2, 0xff2b3b48);
        fill(canvas, 24, 319, 175, 480, 2, 0xff2a3e48);
        fill(canvas, 184, 172, 342, 490, 4, 0xff32505b);
        fill(canvas, 189, 179, 337, 480, 2, 0xff1c2e3b);

        // Roof, collectors, window outlines and room depth.
        polygon(canvas, 0xff2a4148, 8, 155, 98, 72, 195, 155, 183, 169, 98, 91, 19, 166);
        polygon(canvas, 0xff567784, 14, 147, 98, 68, 191, 149, 185, 156, 98, 80, 20, 154);
        polygon(canvas, 0xff213949, 21, 154, 98, 86, 173, 154);
        stroke(canvas, 98, 89, 98, 151, 2, 0xff365468);
        stroke(canvas, 55, 124, 140, 124, 1, 0xff365468);
        fill(canvas, 84, 122, 113, 147, 2, 0xff162c3b);
        stroke(canvas, 98, 122, 98, 146, 1, 0xff4d6c7e);
        stroke(canvas, 85, 134, 112, 134, 1, 0xff4d6c7e);
        int roofScope = canvas.save();
        canvas.translate(104, 73);
        canvas.rotate(41);
        for (int i = 0; i < 3; i++) collector(canvas, 4 + i * 30, -6);
        canvas.restoreToCount(roofScope);
        fill(canvas, 177, 165, 349, 176, 3, 0xff557681);
        fill(canvas, 15, 311, 184, 320, 2, 0xff45616b);
        fill(canvas, 15, 480, 346, 491, 2, 0xff557481);

        text(canvas, "KROVNI SOLARNI KOLEKTORI", 15, 25, 10, GOLD, true, false);
        stroke(canvas, 107, 32, 126, 49, 1, GOLD);
        stroke(canvas, 126, 49, 126, 82, 1, GOLD);
        fill(canvas, 123.5f, 79.5f, 128.5f, 84.5f, 3, GOLD);
        stroke(canvas, 166, 111, 205, 111, 1.8f, PIPE);
        pump(canvas, 219, 111, 4, now);
        text(canvas, HouseTelemetry.degrees(state.collector), 238, 115, 12,
                HouseTelemetry.valueColor(state.collector, GOLD, now), true, false);
        text(canvas, "KOTLOVNICA", 264, 198, 11, BLUE, true, true);

        // Schematic heating path, stationary for all states. Only the pump's arrows move.
        route(canvas, 2.6f, 143, 280, 174, 280, 194, 280, 194, 445, 305, 445);
        route(canvas, 2.6f, 143, 432, 173, 432, 194, 432);
        route(canvas, 1.7f, 196, 379, 196, 365, 305, 365, 305, 345);
        route(canvas, 1.7f, 224, 345, 224, 365);
        wallHighlight(canvas, 156, 184, 170, 218);
        wallHighlight(canvas, 156, 338, 170, 372);
        floor(canvas, 178, "Kat", state.kat, state.katTarget, GOLD, now);
        floor(canvas, 331, "Prizemlje", state.prizemlje, state.prizemljeTarget, BLUE, now);

        buffer(canvas, 195, 208, "2.000 L", state.buffer, now);
        buffer(canvas, 269, 208, "1.000 L", state.bufferTwo, now);
        boiler(canvas, 211, 407);
        sanitary(canvas, 262, 407);
        heatPump(canvas, 312, 407);
        text(canvas, "Peć", 211, 435, 12, INK, true, true);
        text(canvas, "Sanitarna", 262, 435, 11.5f, INK, true, true);
        text(canvas, "Dizalica", 313, 435, 11.5f, INK, true, true);
        // Numbers use the same stale color system as floor and tank readings.
        text(canvas, HouseTelemetry.degrees(state.boiler), 211, 462, 13.5f, HouseTelemetry.valueColor(state.boiler, GOLD, now), true, true);
        text(canvas, HouseTelemetry.degrees(state.sanitary), 262, 462, 13.5f, HouseTelemetry.valueColor(state.sanitary, GOLD, now), true, true);
        text(canvas, HouseTelemetry.degrees(state.heatPump), 312, 462, 13.5f, HouseTelemetry.valueColor(state.heatPump, BLUE, now), true, true);
        pump(canvas, 162, 280, 0, now);
        pump(canvas, 162, 432, 1, now);
        pump(canvas, 211, 381, 2, now);
        pump(canvas, 262, 381, 3, now);

        text(canvas, "Shematski prikaz opreme", 180, 519, 12, MUTED, false, true);
        canvas.restoreToCount(save);
        if (isShown() && getWindowVisibility() == VISIBLE) {
            postInvalidateDelayed(state.hasRunning(now) && android.animation.ValueAnimator.areAnimatorsEnabled() ? 42 : 15000);
        }
    }

    private float baseScale() { return Math.min(getWidth() / W, getHeight() / H); }

    void zoomBy(float factor) {
        float x = getWidth() / 2f, y = getHeight() / 2f;
        zoomAt(factor, x, y, x, y);
    }
    float zoomFactor() { return viewport.zoom; }

    void resetZoom() {
        viewport.zoom = 1f; viewport.x = 0; viewport.y = 0;
        invalidate();
    }

    private void zoomAt(float factor, float previousX, float previousY, float focusX, float focusY) {
        if (!Float.isFinite(factor) || factor <= 0) return;
        float old = viewport.zoom;
        viewport.zoom = Math.max(1f, Math.min(MAX_ZOOM, old * factor));
        float ratio = viewport.zoom / old;
        viewport.x = focusX - getWidth() / 2f + (getWidth() / 2f - previousX + viewport.x) * ratio;
        viewport.y = focusY - getHeight() / 2f + (getHeight() / 2f - previousY + viewport.y) * ratio;
        clampViewport(); invalidate();
    }

    private void clampViewport() {
        float scale = baseScale() * viewport.zoom;
        float maxX = Math.max(0, (W * scale - getWidth()) / 2f), maxY = Math.max(0, (H * scale - getHeight()) / 2f);
        viewport.x = Math.max(-maxX, Math.min(maxX, viewport.x));
        viewport.y = Math.max(-maxY, Math.min(maxY, viewport.y));
    }

    @Override public boolean onTouchEvent(MotionEvent event) {
        int action = event.getActionMasked();
        if (action == MotionEvent.ACTION_DOWN) {
            pinchFirst = pinchSecond = MotionEvent.INVALID_POINTER_ID;
            if (viewport.zoom > 1f) disallowScroll(true);
            dragId = event.getPointerId(0); dragX = event.getX(); dragY = event.getY();
        } else if (action == MotionEvent.ACTION_POINTER_DOWN) {
            disallowScroll(true);
            if (pinchSecond == MotionEvent.INVALID_POINTER_ID) beginPinch(event, -1);
        } else if (action == MotionEvent.ACTION_POINTER_UP) {
            int lifted = event.getActionIndex();
            if (event.getPointerId(lifted) == pinchFirst || event.getPointerId(lifted) == pinchSecond) beginPinch(event, lifted);
        } else if (action == MotionEvent.ACTION_MOVE) {
            if (pinchSecond != MotionEvent.INVALID_POINTER_ID) {
                int first = event.findPointerIndex(pinchFirst), second = event.findPointerIndex(pinchSecond);
                if (first >= 0 && second >= 0) trackPinch(event, first, second, true);
            } else if (event.getPointerCount() == 1) {
                int index = event.findPointerIndex(dragId);
                if (index >= 0) {
                    float x = event.getX(index), y = event.getY(index);
                    if (viewport.zoom > 1f) {
                        viewport.x += x - dragX; viewport.y += y - dragY;
                        clampViewport(); invalidate();
                    }
                    dragX = x; dragY = y;
                }
            }
        } else if (action == MotionEvent.ACTION_UP || action == MotionEvent.ACTION_CANCEL) {
            dragId = pinchFirst = pinchSecond = MotionEvent.INVALID_POINTER_ID;
            disallowScroll(false);
            if (action == MotionEvent.ACTION_UP) performClick();
        }
        return true;
    }

    @Override public boolean performClick() { return super.performClick(); }

    private void disallowScroll(boolean disallow) {
        if (getParent() != null) getParent().requestDisallowInterceptTouchEvent(disallow);
    }

    private void beginPinch(MotionEvent event, int excludedIndex) {
        int first = -1, second = -1;
        for (int i = 0; i < event.getPointerCount(); i++) {
            if (i == excludedIndex) continue;
            if (first < 0) first = i;
            else { second = i; break; }
        }
        pinchFirst = first < 0 ? MotionEvent.INVALID_POINTER_ID : event.getPointerId(first);
        pinchSecond = second < 0 ? MotionEvent.INVALID_POINTER_ID : event.getPointerId(second);
        if (second >= 0) trackPinch(event, first, second, false);
        else if (first >= 0) {
            dragId = pinchFirst; dragX = event.getX(first); dragY = event.getY(first);
            if (viewport.zoom <= 1f) disallowScroll(false);
        }
    }

    private void trackPinch(MotionEvent event, int first, int second, boolean adjust) {
        float x1 = event.getX(first), y1 = event.getY(first), x2 = event.getX(second), y2 = event.getY(second);
        float x = (x1 + x2) / 2f, y = (y1 + y2) / 2f;
        float span = (float) Math.hypot(x1 - x2, y1 - y2);
        float minimum = 8 * getResources().getDisplayMetrics().density;
        if (adjust && pinchSpan >= minimum && span >= minimum) zoomAt(span / pinchSpan, pinchX, pinchY, x, y);
        pinchX = x; pinchY = y; pinchSpan = span;
    }

    private void collector(Canvas c, float x, float y) {
        fill(c, x, y, x + 27, y + 17, 2, 0xff77aabd);
        fill(c, x + 1.5f, y + 1.5f, x + 25.5f, y + 15.5f, 1, 0xff284a63);
        for (int i = 1; i <= 3; i++) stroke(c, x + 6.75f * i, y + 2, x + 6.75f * i, y + 15, .6f, 0xffa0c7da);
        stroke(c, x + 2, y + 8.5f, x + 25, y + 8.5f, .75f, 0xffa0c7da);
    }

    private void wallHighlight(Canvas c, float l, float t, float r, float b) {
        fill(c, l - 1, t - 1, r + 1, b + 1, 2, 0xff6a8891);
        fill(c, l, t, r, b, 2, 0xff2a536f);
        stroke(c, (l + r) / 2, t, (l + r) / 2, b, 1, 0xff6a8891);
        stroke(c, l, (t + b) / 2, r, (t + b) / 2, 1, 0xff6a8891);
    }

    private void floor(Canvas c, float y, String name, HouseTelemetry.Reading measured, HouseTelemetry.Reading target, int accent, Instant now) {
        text(c, name, 37, y + 7, 17, INK, true, false);
        text(c, HouseTelemetry.degrees(measured), 36, y + 47, 29, HouseTelemetry.valueColor(measured, accent, now), true, false);
        text(c, "Izmjereno", 37, y + 64, 12, MUTED, false, false);
        text(c, "Zadano  " + HouseTelemetry.degrees(target), 37, y + 100, 14, HouseTelemetry.valueColor(target, INK, now), true, false);
        // A small radiator on the same schematic circulation loop.
        fill(c, 99, y + 113, 143, y + 124, 2, 0xff546777);
        for (int x = 105; x < 143; x += 5) stroke(c, x, y + 115, x, y + 122, 1, 0xff8b9ba4);
    }

    private void buffer(Canvas c, float x, float y, String capacity, HouseTelemetry.Reading[] values, Instant now) {
        int saved = c.save();
        c.translate(x, y);
        fill(c, 0, 0, 64, 144, 13, 0xff42626a);
        paint.setShader(tank);
        c.drawRoundRect(1.5f, 1.5f, 62.5f, 142.5f, 12, 12, paint);
        paint.setShader(null);
        text(c, "Pufer", 32, 17, 11.5f, MUTED, true, true);
        text(c, capacity, 32, 35, 15, INK, true, true);
        String[] names = {"Vrh", "Sredina", "Dno"};
        for (int n = 0; n < 3; n++) {
            float line = 49 + n * 30;
            stroke(c, 5, line - 5, 59, line - 5, 1, FRAME);
            text(c, names[n], 32, line + 7, 11, MUTED, false, true);
            text(c, HouseTelemetry.degrees(values[n]), 32, line + 23, 14, HouseTelemetry.valueColor(values[n], GOLD, now), true, true);
        }
        c.restoreToCount(saved);
    }

    private void boiler(Canvas c, float x, float y) {
        fill(c, x - 15, y - 9, x + 15, y + 15, 4, 0xff56636a);
        fill(c, x - 11, y - 7, x + 11, y + 11, 3, 0xff253743);
        polygon(c, GOLD, x - 5, y + 7, x - 5, y + 1, x + 1, y - 5, x + 3, y, x + 6, y + 4, x + 4, y + 8);
    }

    private void sanitary(Canvas c, float x, float y) {
        fill(c, x - 11, y - 10, x + 11, y + 14, 7, 0xff527182);
        fill(c, x - 8, y - 8, x + 8, y + 12, 5, 0xff375161);
        polygon(c, BLUE, x, y - 4, x - 4, y + 3, x - 2, y + 7, x + 2, y + 7, x + 4, y + 3);
    }

    private void heatPump(Canvas c, float x, float y) {
        fill(c, x - 16, y - 9, x + 16, y + 15, 3, 0xff566b78);
        fill(c, x - 14, y - 7, x + 14, y + 13, 2, 0xff324958);
        fill(c, x - 11, y - 4, x + 3, y + 10, 8, 0xff222f3f);
        paint.setColor(0xff84a4b2);
        c.drawCircle(x - 4, y + 3, 3, paint);
        stroke(c, x + 6, y - 3, x + 12, y - 3, 1, BLUE);
        stroke(c, x + 6, y + 1, x + 12, y + 1, 1, BLUE);
        stroke(c, x + 6, y + 5, x + 12, y + 5, 1, BLUE);
    }

    private void pump(Canvas c, float x, float y, int index, Instant now) {
        HouseTelemetry.Pump mode = state.pumpState(index, now);
        boolean fresh = state.pumpReadings[index].age(now) == HouseTelemetry.Age.FRESH;
        int color = mode == HouseTelemetry.Pump.ON ? (fresh ? GREEN : 0xff83a899)
                : mode == HouseTelemetry.Pump.OFF ? (fresh ? RED : 0xffbda0a3) : UNKNOWN;
        paint.setColor(0xff1c2e36);
        c.drawCircle(x, y, 12, paint);
        paint.setStyle(Paint.Style.STROKE);
        paint.setStrokeWidth(1);
        paint.setColor(color);
        c.drawCircle(x, y, 11.5f, paint);
        paint.setStyle(Paint.Style.FILL);
        if (mode == HouseTelemetry.Pump.UNKNOWN) {
            text(c, "?", x, y + 4, 12, color, true, true);
            return;
        }
        int saved = c.save();
        if (mode == HouseTelemetry.Pump.ON && fresh && android.animation.ValueAnimator.areAnimatorsEnabled()) {
            c.rotate((SystemClock.uptimeMillis() % 1700) * 360f / 1700, x, y);
        }
        paint.setStyle(Paint.Style.STROKE);
        paint.setStrokeWidth(1.6f);
        paint.setStrokeCap(Paint.Cap.ROUND);
        c.drawArc(x - 6.3f, y - 6.3f, x + 6.3f, y + 6.3f, -135, 120, false, paint);
        c.drawArc(x - 6.3f, y - 6.3f, x + 6.3f, y + 6.3f, 45, 120, false, paint);
        paint.setStyle(Paint.Style.FILL);
        polygon(c, color, x + 4, y - 2.3f, x + 8, y - 3, x + 6.2f, y + 1.8f);
        polygon(c, color, x - 4, y + 2.3f, x - 8, y + 3, x - 6.2f, y - 1.8f);
        c.restoreToCount(saved);
    }

    private void route(Canvas c, float width, float... points) {
        paint.setStrokeJoin(Paint.Join.ROUND);
        paint.setStrokeCap(Paint.Cap.ROUND);
        paint.setStyle(Paint.Style.STROKE);
        paint.setColor(PIPE);
        paint.setStrokeWidth(width);
        path.reset();
        path.moveTo(points[0], points[1]);
        for (int i = 2; i < points.length; i += 2) path.lineTo(points[i], points[i + 1]);
        c.drawPath(path, paint);
        paint.setStyle(Paint.Style.FILL);
    }

    private void fill(Canvas c, float l, float t, float r, float b, float radius, int color) {
        paint.setColor(color);
        c.drawRoundRect(l, t, r, b, radius, radius, paint);
    }

    private void stroke(Canvas c, float x, float y, float x2, float y2, float width, int color) {
        paint.setColor(color);
        paint.setStrokeWidth(width);
        c.drawLine(x, y, x2, y2, paint);
    }

    private void polygon(Canvas c, int color, float... points) {
        path.reset();
        path.moveTo(points[0], points[1]);
        for (int i = 2; i < points.length; i += 2) path.lineTo(points[i], points[i + 1]);
        path.close();
        paint.setColor(color);
        c.drawPath(path, paint);
    }

    private void text(Canvas c, String value, float x, float y, float size, int color, boolean bold, boolean center) {
        paint.setColor(color);
        paint.setTypeface(bold ? BOLD : Typeface.DEFAULT);
        paint.setTextAlign(center ? Paint.Align.CENTER : Paint.Align.LEFT);
        paint.setTextSize(size);
        c.drawText(value, x, y, paint);
    }
}
