package se.automatiziraj.termodom;
import android.content.Context;
import java.io.InputStream;
import java.io.ByteArrayOutputStream;
import java.nio.charset.StandardCharsets;
import java.time.Instant;
import java.time.LocalDateTime;
import java.time.ZoneId;
import org.json.JSONArray;
import org.json.JSONObject;
/** Development transport has no sockets and rejects every mutation. */
public final class DevelopmentMode {
    public static boolean enabled(){return true;}
    public static HeatingRepository repository(Context context){
        return new HeatingRepository((uri,headers)->{
            String name=uri.getPath().substring(uri.getPath().lastIndexOf('/')+1);
            if(uri.getRawQuery()!=null || name.equals("dodajPODESAVANJA.php") || name.equals("zapisi_postavke.php") || name.equals("logout.scr.php"))
                throw new IllegalStateException("DEMO · spremanje u stvarni regulator nije dopušteno.");
            try(InputStream in=context.getAssets().open("fixtures/"+name+".html");ByteArrayOutputStream out=new ByteArrayOutputStream()){
                byte[] buffer=new byte[8192];int n;while((n=in.read(buffer))!=-1)out.write(buffer,0,n);
                return new HeatingRepository.HttpResponse(name.equals("grafP1.php")||name.equals("grafp1-6.php")?404:200,out.toString(StandardCharsets.UTF_8.name()));
            }
        });
    }
    public static JSONObject solarData(){
        try{return new JSONObject().put("stationName","DEMO · testna elektrana").put("fetchedAt","2026-10-22T12:00:00Z")
            .put("powerKw",3.2).put("dailyKwh",18.7).put("monthKwh",436.2).put("yearKwh",5032.8).put("totalKwh",12784.6);}
        catch(Exception e){throw new IllegalStateException(e);}
    }
    static HouseTelemetry.Packet houseData(boolean pumps) throws Exception {
        // The demo covers live, muted, old and unknown display states without using sockets.
        Instant now=Instant.now();
        LocalDateTime time=LocalDateTime.ofInstant(now,ZoneId.of("Europe/Zagreb")).withNano(0);
        JSONObject packet=new JSONObject().put("success",true).put("server_time",time.toString());
        if(pumps){
            JSONArray list=new JSONArray();
            String[] ids={"kat","prizemlje","kotao","sanitarna","solar"};
            int[] states={1,0,1,0,1}, seconds={8,11,9,400,14};
            for(int i=0;i<ids.length;i++)list.put(new JSONObject().put("pump",ids[i]).put("state",states[i]).put("updated_at",time.minusSeconds(seconds[i]).toString()));
            packet.put("pumps",list);
        }else{
            packet.put("room_latest",new JSONArray()
                    .put(new JSONObject().put("room","kat").put("temperature",22.8).put("setpoint",23).put("received_at",time.minusSeconds(14).toString()))
                    .put(new JSONObject().put("room","prizemlje").put("temperature",21.4).put("setpoint",22).put("received_at",time.minusSeconds(18).toString())));
            JSONArray list=new JSONArray();
            String[] topics={"house/outdoor/temp","house/kotal/temp","house/dhw/temp","house/solar/temp","house/puffer/top","house/puffer/midd","house/puffer/bottom","demo/buffer2/top","demo/buffer2/midd","demo/buffer2/bottom","demo/heat-pump"};
            double[] values={8.7,64.5,47.1,32.5,57.6,51.1,43.8,45.2,38.2,32.6,39.0};
            int[] seconds={12,9,40,45,12,12,12,49,210,610,25};
            for(int i=0;i<topics.length;i++)list.put(new JSONObject().put("topic",topics[i]).put("temperature",values[i]).put("received_at",time.minusSeconds(seconds[i]).toString()));
            packet.put("latest",list);
        }
        return new HouseTelemetry.Packet(packet,now);
    }

    static OzaljWeather.Reading weatherData() throws Exception {
        LocalDateTime now=LocalDateTime.now(ZoneId.of("Europe/Zagreb"));
        return new OzaljWeather.Reading(new JSONObject()
                .put("current",new JSONObject().put("time",now.toString()).put("temperature_2m",9.8).put("weather_code",2)
                        .put("apparent_temperature",8.5).put("relative_humidity_2m",76).put("wind_speed_10m",9).put("pressure_msl",1016).put("is_day",1))
                .put("daily",new JSONObject().put("temperature_2m_min",new JSONArray().put(4.8))
                        .put("temperature_2m_max",new JSONArray().put(12.1)).put("precipitation_probability_max",new JSONArray().put(22))
                        .put("sunrise",new JSONArray().put(now.toLocalDate().atTime(7,4).toString()))
                        .put("sunset",new JSONArray().put(now.toLocalDate().atTime(16,28).toString()))));
    }
}
