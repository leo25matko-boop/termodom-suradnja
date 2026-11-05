package se.automatiziraj.termodom;
import android.content.Context;
import org.json.JSONObject;
/** Release builds retain the original native connection, with no embedded account. */
public final class DevelopmentMode {
    public static boolean enabled(){return false;}
    public static HeatingRepository repository(Context context){return new HeatingRepository();}
    public static JSONObject solarData(){return null;}
}
