package se.automatiziraj.termodom;
import android.content.Context;
import java.io.InputStream;
import java.io.ByteArrayOutputStream;
import java.nio.charset.StandardCharsets;
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
}
