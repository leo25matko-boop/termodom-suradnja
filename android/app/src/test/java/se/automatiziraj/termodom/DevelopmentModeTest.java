package se.automatiziraj.termodom;
import org.junit.Test;
import static org.junit.Assert.*;
import java.lang.reflect.Field;
import java.net.URI;
import java.util.Collections;

public class DevelopmentModeTest {
    @Test public void debugModeIsExplicitlyDemo() throws Exception {
        assertTrue(DevelopmentMode.enabled());
        assertTrue(DevelopmentMode.solarData().getString("stationName").contains("DEMO"));
    }
    @Test public void transportRejectsMutationsBeforeUsingContextOrNetwork() throws Exception {
        HeatingRepository repository=DevelopmentMode.repository(null);
        Field field=HeatingRepository.class.getDeclaredField("transport");field.setAccessible(true);
        HeatingRepository.Transport transport=(HeatingRepository.Transport)field.get(repository);
        for(String suffix:new String[]{"dodajPODESAVANJA.php","zapisi_postavke.php","logout.scr.php","novo.php?n0=10"}) {
            try { transport.get(URI.create("https://automatiziraj.se/centralno/php/"+suffix),Collections.emptyMap());fail("Mutation accepted"); }
            catch(IllegalStateException expected) { assertTrue(expected.getMessage().contains("DEMO")); }
        }
    }
}
