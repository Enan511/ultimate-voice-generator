package com.ultimatevoicegenerator;
import org.junit.Test;
import org.json.*;
import static org.junit.Assert.*;
public class TextRulesTest {
 @Test public void punctuationIsPreserved(){assertEquals("Good morning, sir! Don't skip [this]—ready?",TextRules.clean(" Good morning, sir!  Don't skip [this]—ready? "));}
 @Test public void droppedSirFails()throws Exception{JSONObject c=TextRules.compare("Good morning sir","Good morning",new JSONArray());assertFalse(c.getBoolean("passed"));assertTrue(c.getJSONArray("differences").toString().contains("sir"));}
 @Test public void caseAndPunctuationDoNotFail()throws Exception{assertTrue(TextRules.compare("Don't stop, sir!","dont stop sir",new JSONArray()).getBoolean("passed"));}
 @Test public void rulesUseLongestNonCascadingMatch()throws Exception{JSONArray rules=new JSONArray("[{\"word\":\"API key\",\"sayAs\":\"access key\"},{\"word\":\"API\",\"sayAs\":\"A P I\"},{\"word\":\"access\",\"sayAs\":\"entry\"}]");assertEquals("access key and A P I, not APICAL",TextRules.pronounce("API key and API, not APICAL",rules));}
 @Test public void literalTranscriptWinsBeforeLexicon()throws Exception{JSONArray rules=new JSONArray("[{\"word\":\"API\",\"sayAs\":\"A P I\"}]");assertTrue(TextRules.compare("A P I is ready","A P I is ready",rules).getBoolean("passed"));}
}
