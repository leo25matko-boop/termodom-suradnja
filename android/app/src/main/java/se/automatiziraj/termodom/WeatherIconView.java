package se.automatiziraj.termodom;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Path;
import android.view.View;

/** Compact weather symbol driven by the reported WMO weather code. */
final class WeatherIconView extends View {
    private static final int AMBER=0xffedbf79, CLOUD=0xffb0c9da, RAIN=0xff7ec4e4, BG=0xff182432;
    private final Paint paint=new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Path shape=new Path();
    private final int code;
    private final boolean day;

    WeatherIconView(Context context,OzaljWeather.Reading weather){
        super(context);
        code=weather==null?-1:weather.code;
        day=weather!=null&&weather.isDay;
        setImportantForAccessibility(IMPORTANT_FOR_ACCESSIBILITY_NO); // The adjacent label names the conditions.
    }

    @Override protected void onDraw(Canvas canvas){
        super.onDraw(canvas);
        float scale=Math.min(getWidth(),getHeight())/32f;
        int save=canvas.save();canvas.scale(scale,scale);
        paint.setStyle(Paint.Style.FILL);
        if(code>=0&&code<=2){
            float x=code==0?16:11,y=code==0?16:11,r=code==0?6:5;
            paint.setColor(day?AMBER:CLOUD);canvas.drawCircle(x,y,r,paint);
            if(day){
                paint.setStrokeWidth(1.7f);paint.setStrokeCap(Paint.Cap.ROUND);
                for(int i=0;i<8;i++){
                    double a=Math.PI*i/4;
                    float dx=(float)Math.cos(a),dy=(float)Math.sin(a);
                    canvas.drawLine(x+dx*(r+3),y+dy*(r+3),x+dx*(r+5),y+dy*(r+5),paint);
                }
            }else{paint.setColor(BG);canvas.drawCircle(x+2.6f,y-2.2f,r*.9f,paint);}
        }
        if(code!=0){
            boolean fog=code==45||code==48;
            boolean snow=code>=71&&code<=77||code==85||code==86;
            paint.setColor(code<0?0xff617486:CLOUD);
            canvas.drawRoundRect(5,15,29,23,4,4,paint);
            canvas.drawCircle(15.5f,14.5f,6,paint);
            canvas.drawCircle(23,15.5f,5,paint);
            if(fog){
                paint.setStrokeWidth(1.5f);paint.setColor(CLOUD);
                canvas.drawLine(8,26,25,26,paint);canvas.drawLine(11,29,22,29,paint);
            }else if(snow){
                paint.setColor(CLOUD);canvas.drawCircle(10,27,1.7f,paint);canvas.drawCircle(19,29,1.7f,paint);canvas.drawCircle(26,26,1.7f,paint);
            }else if(code>=95){
                paint.setColor(AMBER);shape.reset();shape.moveTo(18,20);shape.lineTo(13,26);shape.lineTo(17,26);shape.lineTo(15,32);shape.lineTo(22,24);shape.lineTo(18,24);shape.close();canvas.drawPath(shape,paint);
            }else if(code>=51){
                paint.setColor(RAIN);paint.setStrokeWidth(1.7f);paint.setStrokeCap(Paint.Cap.ROUND);
                canvas.drawLine(12,25,10,29,paint);canvas.drawLine(19,26,17,30,paint);canvas.drawLine(26,25,24,29,paint);
            }
        }
        canvas.restoreToCount(save);
    }
}
