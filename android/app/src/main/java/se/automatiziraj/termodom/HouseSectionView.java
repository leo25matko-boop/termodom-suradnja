package se.automatiziraj.termodom;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Rect;
import android.graphics.RectF;
import android.graphics.Typeface;
import android.view.MotionEvent;
import android.view.View;

import java.time.Instant;

/** The original cutaway, with room readings anchored to the illustrated floors. */
final class HouseSectionView extends View {
    private static final float W = 360, MAX_ZOOM = 3f;
    private static final int INK = 0xffeef5f5, MUTED = 0xffbdcbd0,
            GREEN = 0xff87e1bd, GOLD = 0xfff1c381, BLUE = 0xffb4d9ed;
    private static final Typeface BOLD = Typeface.create("sans-serif-medium", Typeface.NORMAL);
    // Pixel coordinates in the 1536 × 1024 cutaway resource. The boiler room
    // has its own section below the house on the narrow Android layout.
    private static final Rect HOUSE = new Rect(0, 38, 1055, 918);
    private static final Rect ANNEX = new Rect(1017, 547, 1536, 918);
    private static final float H = W * HOUSE.height() / HOUSE.width();
    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG | Paint.FILTER_BITMAP_FLAG);
    private final RectF houseBounds = new RectF(0, 0, W, H);
    private final HouseTelemetry state;
    private final Bitmap artwork;
    static final class Viewport { float zoom = 1f, x, y; }
    private final Viewport viewport;
    private Runnable onZoomChanged;
    private int dragId = MotionEvent.INVALID_POINTER_ID, pinchFirst = MotionEvent.INVALID_POINTER_ID,
            pinchSecond = MotionEvent.INVALID_POINTER_ID;
    private float dragX, dragY, pinchSpan, pinchX, pinchY;

    HouseSectionView(Context context, HouseTelemetry state, Viewport viewport, Bitmap artwork) {
        super(context);
        this.state = state;
        this.viewport = viewport;
        this.artwork = artwork;
        setImportantForAccessibility(IMPORTANT_FOR_ACCESSIBILITY_YES);
        setContentDescription(state.description());
        setTag("house-section");
    }

    static View boilerRoomIllustration(Context context, Bitmap artwork) {
        View illustration = new View(context) {
            private final Paint imagePaint = new Paint(Paint.ANTI_ALIAS_FLAG | Paint.FILTER_BITMAP_FLAG);
            private final RectF bounds = new RectF();

            @Override
            protected void onMeasure(int widthSpec, int heightSpec) {
                int width = View.MeasureSpec.getSize(widthSpec);
                float maxWidth = 244 * getResources().getDisplayMetrics().density;
                int height = Math.round(Math.min(width, maxWidth) * ANNEX.height() / ANNEX.width());
                setMeasuredDimension(width, resolveSize(height, heightSpec));
            }

            @Override
            protected void onDraw(Canvas canvas) {
                float width = getHeight() * (float) ANNEX.width() / ANNEX.height();
                float left = (getWidth() - width) / 2;
                bounds.set(left, 0, left + width, getHeight());
                canvas.drawBitmap(artwork, ANNEX, bounds, imagePaint);
            }
        };
        illustration.setImportantForAccessibility(IMPORTANT_FOR_ACCESSIBILITY_NO);
        return illustration;
    }

    @Override
    protected void onMeasure(int widthSpec, int heightSpec) {
        int available = View.MeasureSpec.getSize(widthSpec);
        int maxWidth = Math.round(540 * getResources().getDisplayMetrics().density);
        int drawing = Math.min(maxWidth, available);
        setMeasuredDimension(available, resolveSize(Math.round(drawing * H / W), heightSpec));
    }

    @Override
    protected void onDraw(Canvas canvas) {
        super.onDraw(canvas);
        float scale = baseScale() * viewport.zoom;
        if (scale <= 0) return;
        clampViewport();
        Instant now = Instant.now();
        int save = canvas.save();
        canvas.translate((getWidth() - W * scale) / 2 + viewport.x,
                         (getHeight() - H * scale) / 2 + viewport.y);
        canvas.scale(scale, scale);
        paint.setColor(0xffffffff);
        canvas.drawBitmap(artwork, HOUSE, houseBounds, paint);

        fill(canvas, 158, 6, 345, 32, 6, 0xff29444c);
        fill(canvas, 159, 7, 344, 31, 5, 0xff172a34);
        text(canvas, "Solarni kolektori", 168, 23, 12, INK, false);
        text(canvas, HouseTelemetry.degrees(state.collector.value), 275, 23, 14,
                HouseTelemetry.valueColor(state.collector, GOLD, now), true);
        paint.setColor(0xff7f9da4);
        paint.setStrokeWidth(1);
        canvas.drawLine(273, 32, 273, 41, paint);

        floor(canvas, 111, "1. kat", state.kat, state.katTarget, state.katSupply, now);
        floor(canvas, 211, "Prizemlje", state.prizemlje, state.prizemljeTarget, state.prizemljeSupply, now);
        canvas.restoreToCount(save);
        if (isShown() && getWindowVisibility() == VISIBLE) postInvalidateDelayed(15000);
    }

    private void floor(Canvas canvas, float top, String name, HouseTelemetry.Reading measured,
                       HouseTelemetry.Reading target, HouseTelemetry.Reading supply, Instant now) {
        fill(canvas, 42, top, 222, top + 72, 7, 0xf21a2c34);
        text(canvas, name, 52, top + 15, 14, INK, true);
        text(canvas, "Sobna", 52, top + 31, 10.5f, MUTED, false);
        text(canvas, "Zadana", 134, top + 31, 10.5f, MUTED, false);
        text(canvas, HouseTelemetry.degrees(measured.value), 52, top + 51, 17,
                HouseTelemetry.valueColor(measured, GREEN, now), true);
        text(canvas, HouseTelemetry.degrees(target.value), 134, top + 51, 17,
                HouseTelemetry.valueColor(target, GREEN, now), true);
        text(canvas, "Polaz", 52, top + 65, 10.5f, MUTED, false);
        text(canvas, HouseTelemetry.degrees(supply.value), 86, top + 65, 11,
                HouseTelemetry.valueColor(supply, BLUE, now), true);
    }

    private float baseScale() { return Math.min(getWidth() / W, getHeight() / H); }

    void zoomBy(float factor) {
        float x = getWidth() / 2f, y = getHeight() / 2f;
        zoomAt(factor, x, y, x, y);
    }
    float zoomFactor() { return viewport.zoom; }
    void onZoomChanged(Runnable callback) { onZoomChanged = callback; }

    void resetZoom() {
        viewport.zoom = 1f; viewport.x = 0; viewport.y = 0;
        invalidate();
        if (onZoomChanged != null) onZoomChanged.run();
    }

    private void zoomAt(float factor, float previousX, float previousY, float focusX, float focusY) {
        if (!Float.isFinite(factor) || factor <= 0) return;
        float old = viewport.zoom;
        viewport.zoom = Math.max(1f, Math.min(MAX_ZOOM, old * factor));
        float ratio = viewport.zoom / old;
        viewport.x = focusX - getWidth() / 2f + (getWidth() / 2f - previousX + viewport.x) * ratio;
        viewport.y = focusY - getHeight() / 2f + (getHeight() / 2f - previousY + viewport.y) * ratio;
        clampViewport(); invalidate();
        if (viewport.zoom != old && onZoomChanged != null) onZoomChanged.run();
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

    private void fill(Canvas canvas, float left, float top, float right, float bottom, float radius, int color) {
        paint.setColor(color);
        canvas.drawRoundRect(left, top, right, bottom, radius, radius, paint);
    }

    private void text(Canvas canvas, String value, float x, float y, float size, int color, boolean bold) {
        paint.setColor(color);
        paint.setTypeface(bold ? BOLD : Typeface.DEFAULT);
        paint.setTextAlign(Paint.Align.LEFT);
        paint.setTextSize(size);
        canvas.drawText(value, x, y, paint);
    }
}
