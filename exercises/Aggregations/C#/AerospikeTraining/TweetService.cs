﻿/*******************************************************************************
 * Copyright 2012-2014 by Aerospike.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to
 * deal in the Software without restriction, including without limitation the
 * rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
 * sell copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
 * IN THE SOFTWARE.
 ******************************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Aerospike.Client;

namespace AerospikeTraining
{
    class TweetService
    {
        private AerospikeClient client;

        public TweetService(AerospikeClient c)
        {
            this.client = c;
        }

        public void createTweet()
        {
            Console.WriteLine("\n********** Create Tweet **********\n");

            Record userRecord = null;
            Key userKey = null;
            Key tweetKey = null;

            // Get username
            string username;
            Console.WriteLine("\nEnter username:");
            username = Console.ReadLine();

            if (username != null && username.Length > 0)
            {
                // Check if username exists
                userKey = new Key("test", "users", username);
                userRecord = client.Get(null, userKey);
                if (userRecord != null)
                {
                    int nextTweetCount = int.Parse(userRecord.GetValue("tweetcount").ToString()) + 1;

                    // Get tweet
                    string tweet;
                    Console.WriteLine("Enter tweet for " + username + ":");
                    tweet = Console.ReadLine();

                    // Write record
                    WritePolicy wPolicy = new WritePolicy();
                    wPolicy.recordExistsAction = RecordExistsAction.UPDATE;

                    // Create timestamp to store along with the tweet so we can query, index and report on it
                    long ts = getTimeStamp();

                    tweetKey = new Key("test", "tweets", username + ":" + nextTweetCount);
                    Bin bin1 = new Bin("tweet", tweet);
                    Bin bin2 = new Bin("ts", ts);
                    Bin bin3 = new Bin("username", username);

                    client.Put(wPolicy, tweetKey, bin1, bin2, bin3);
                    Console.WriteLine("\nINFO: Tweet record created!");

                    // Update tweet count and last tweet'd timestamp in the user record
                    updateUser(client, userKey, wPolicy, ts, nextTweetCount);
                }
                else
                {
                    Console.WriteLine("ERROR: User record not found!");
                }
            }
        } //createTweet

        private void updateUser(AerospikeClient client, Key userKey, WritePolicy policy, long ts, int tweetCount)
        {
            // Update tweet count and last tweet'd timestamp in the user record
            client.Put(policy, userKey, new Bin("tweetcount", tweetCount), new Bin("lasttweeted", ts));
            Console.WriteLine("INFO: The tweet count now is: " + tweetCount);
        } //updateUser

        public void scanAllTweetsForAllUsers()
        {
            ScanPolicy policy = new ScanPolicy();
            policy.concurrentNodes = true;
            policy.priority = Priority.LOW;
            policy.includeBinData = true;
            client.ScanAll(policy, "test", "tweets", scanTweetsCallback, "tweet");
        } //scanAllTweets

        public void scanTweetsCallback(Key key, Record record)
        {
            Console.WriteLine(record.GetValue("tweet"));
        } //scanTweetsCallback

        public void queryTweetsByUsername()
        {
            Console.WriteLine("\n********** Query Tweets By Username **********\n");

            RecordSet rs = null;
            try
            {
                // Get username
                string username;
                Console.WriteLine("\nEnter username:");
                username = Console.ReadLine();

                if (username != null && username.Length > 0)
                {
                    // NOTE: Index creation has been included in here for convenience and to demonstrate the syntax
                    // NOTE: The recommended way of creating indexes in production env is via AQL
                    IndexTask task = client.CreateIndex(null, "test", "tweets", "username_index", "username", IndexType.STRING);
                    task.Wait();
                    
                    string[] bins = { "tweet" };
                    Statement stmt = new Statement();
                    stmt.SetNamespace("test");
                    stmt.SetSetName("tweets");
                    stmt.SetIndexName("username_index");
                    stmt.SetBinNames(bins);
                    stmt.SetFilters(Filter.Equal("username", username));

                    Console.WriteLine("\nHere's " + username + "'s tweet(s):\n");

                    rs = client.Query(null, stmt);
                    while (rs.Next())
                    {
                        Record r = rs.Record;
                        Console.WriteLine(r.GetValue("tweet"));
                    }
                }
                else
                {
                    Console.WriteLine("ERROR: User record not found!");
                }
            }
            finally
            {
                if (rs != null)
                {
                    // Close record set
                    rs.Close();
                }
            }
        } //queryTweetsByUsername

        public void queryUsersByTweetCount()
        {
            Console.WriteLine("\n********** Query Users By Tweet Count Range **********\n");

            RecordSet rs = null;
            try
            {
                // NOTE: Index creation has been included in here for convenience and to demonstrate the syntax
                // NOTE: The recommended way of creating indexes in production env is via AQL
                IndexTask task = client.CreateIndex(null, "test", "users", "tweetcount_index", "tweetcount", IndexType.NUMERIC);
                task.Wait();

                // Get min and max tweet counts
                int min;
                int max;
                Console.WriteLine("\nEnter Min Tweet Count:");
                min = int.Parse(Console.ReadLine());
                Console.WriteLine("Enter Max Tweet Count:");
                max = int.Parse(Console.ReadLine());

                string[] bins = { "username", "tweetcount" };
                Statement stmt = new Statement();
                stmt.SetNamespace("test");
                stmt.SetSetName("users");
                stmt.SetIndexName("tweetcount_index");
                stmt.SetBinNames(bins);
                stmt.SetFilters(Filter.Range("tweetcount", min, max));

                Console.WriteLine("\nList of users with " + min + "-" + max + " tweets:\n");

                rs = client.Query(null, stmt);
                while (rs.Next())
                {
                    Record r = rs.Record;
                    Console.WriteLine(r.GetValue("username") + " has " + r.GetValue("tweetcount") + " tweets");
                }
            }
            finally
            {
                if (rs != null)
                {
                    // Close record set
                    rs.Close();
                }
            }
        } //queryUsersByTweetCount

        public void createTweets()
        {
            string[] randomTweets = { "For just $1 you get a half price download of half of the song and listen to it just once.", "People tell me my body looks like a melted candle", "Come on movie! Make it start!", "Byaaaayy", "Please, please, win! Meow, meow, meow!", "Put. A. Bird. On. It.", "A weekend wasted is a weekend well spent", "Would you like to super spike your meal?", "We have a mean no-no-bring-bag up here on aisle two.", "SEEK: See, Every, EVERY, Kind... of spot", "We can order that for you. It will take a year to get there.", "If you are pregnant, have a soda.", "Hear that snap? Hear that clap?", "Follow me and I may follow you", "Which is the best cafe in Portland? Discuss...", "Portland Coffee is for closers!", "Lets get this party started!", "How about them portland blazers!", "You got school'd, yo", "I love animals", "I love my dog", "What's up Portland", "Which is the best cafe in Portland? Discuss...", "I dont always tweet, but when I do it is on Tweetaspike" };
            Random rnd1 = new Random();
            Random rnd2 = new Random();
            Random rnd3 = new Random();
            Key userKey;
            Record userRecord;
            int totalUsers = 10000;
            int maxTweets = 20;
            string username;
            long ts = 0;
            WritePolicy wPolicy = new WritePolicy();
            wPolicy.recordExistsAction = RecordExistsAction.UPDATE;

            Console.WriteLine("\nCreate up to " + maxTweets + " tweets each for " + totalUsers + " users. Press any key to continue...");
            Console.ReadLine();

            for (int j = 0; j < totalUsers; j++)
            {
                // Check if user record exists
                username = "user" + rnd3.Next(1, 100000);
                userKey = new Key("test", "users", username);
                userRecord = client.Get(null, userKey);
                if (userRecord != null)
                {
                    // create up to maxTweets random tweets for this user
                    int totalTweets = rnd1.Next(1, (maxTweets + 1));
                    for (int k = 1; k <= totalTweets; k++)
                    {
                        // Create timestamp to store along with the tweet so we can query, index and report on it
                        ts = getTimeStamp();
                        Key tweetKey = new Key("test", "tweets", username + ":" + k);
                        Bin bin1 = new Bin("tweet", randomTweets[rnd2.Next(1, randomTweets.Length)]);
                        Bin bin2 = new Bin("ts", ts);
                        Bin bin3 = new Bin("username", username);

                        client.Put(wPolicy, tweetKey, bin1, bin2, bin3);
                    }
                    if (totalTweets > 0)
                    {
                        // Update tweet count and last tweet'd timestamp in the user record
                        client.Put(wPolicy, userKey, new Bin("tweetcount", totalTweets), new Bin("lasttweeted", ts));
                        //Console.WriteLine("INFO: The tweet count now is: " + totalTweets);
                    }
                    Console.WriteLine("Wrote " + totalTweets + " tweets for " + username + "!");
                }
            }
            Console.WriteLine("\nDone creating up to " + maxTweets + " tweets each for " + totalUsers + " users!");
        } //createTweets

        private long getTimeStamp()
        {
            double epoch = (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            return Convert.ToInt64(epoch);
        } //getTimeStamp

    }
}
