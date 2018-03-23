// Databricks notebook source
// MAGIC %md #Set Storage Config

// COMMAND ----------

//Let's start by mounting the storage so we can refer to this location in the rest of the notebook
dbutils.fs.mount(
  source = "wasbs://lastfmdata@wblastfmstorage.blob.core.windows.net/data/",
  mountPoint = "/mnt/lastfmdata",
  extraConfigs = Map("fs.azure.account.key.wblastfmstorage.blob.core.windows.net" ->  "MYKEY"))


// COMMAND ----------

// MAGIC %md # Read Data

// COMMAND ----------

//Read the exported files from Azure Storage and cache it in-memory
val lastFM = spark.read.json("/mnt/lastfmdata").cache()


// COMMAND ----------

// MAGIC %md # Do some cleanup

// COMMAND ----------

//We are all using cool nicknames so let's transform this to something everyone understands
val userSeq = Seq(
    ("dis4ea", "Wesley"),
    ("master_cobra", "Father"),
    ("laylabee14", "Daughter"))

//Select the fields we are interested in
val users = sc.makeRDD(userSeq).toDF("username", "name")
val lastfmclean = lastFM.select(
lastFM("artist.name") as "artist", 
lastFM("artist.image") as "artist_images", 
lastFM("name") as "song", 
lastFM("album.text") as "album", 
lastFM("date.uts") as "played_at", 
lastFM("user") as "username"
)

lastfmclean.limit(5).show()


// COMMAND ----------

val lastfmwithuser = lastfmclean.join(users, lastfmclean("username") === users("username"))
/*
//I have a cleaning service at export time using AKS so maybe nice for a next blog post ;-)
val songCleaner = udf {(originalName:String) => {
  originalName.replace("(Original)", "")
  .replace("(Original Title)", "")
  .replace("(Remastered Album Version)", "")
  .replace("- Single Version", "")
  .replace("(Extended Mix)", "")
  .replaceAll("\\([Ll]ive.*", "")
  .replaceAll("- Live.*", "")
  .replaceAll("- Album.*", "")
  .replaceAll("- Remaster.*", "")
  .replaceAll("- Remastered.*", "")
  .replaceAll("\\(Original.*", "")
  .trim
}}
*/

//Convert timestamp to something readable
import java.sql.Timestamp
val toTs = udf{(date: String) => new Timestamp(date.toLong * 1000)}


val playsNoTime = lastfmwithuser.select(lastfmclean("artist"), lastfmclean("song"), lastfmclean("album"), toTs(lastfmclean("played_at")) as "played_at", users("name") as "user")

//create a temporary table so we can query using SQL
playsNoTime.createOrReplaceTempView("TempLastFMPlaysNoTime")

//We can also use good old SQL statements
val plays = spark.sql("SELECT artist, song, album, from_utc_timestamp(played_at,'CET') as played_at, user, HOUR(from_utc_timestamp(played_at,'CET')) as timeOfDay FROM TempLastFMPlaysNoTime")


// COMMAND ----------

plays.createOrReplaceTempView("TempLastFMPlays")

display(spark.sql("SELECT user, COUNT(*) as Plays FROM TempLastFMPlays GROUP BY user ORDER BY Plays DESC"))
//display(spark.sql("SELECT * FROM TempLastFMPlays ORDER BY played_at DESC"))
//display(spark.sql("SELECT user, artist, COUNT(*) as Plays FROM TempLastFMPlays GROUP BY user, artist ORDER BY Plays DESC"))


// COMMAND ----------

plays.write.mode(SaveMode.Overwrite).saveAsTable("LastFMPlays")

// COMMAND ----------

//Let's unmount the storage
dbutils.fs.unmount("/mnt/lastfmdata")
