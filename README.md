Microtools
=============

My F# project with real results ;)

TwitterConversation
-------

Ever wanted to see grouped twitter conversation? Yes? Then, try TwitterConversation.exe. You first need to register your account (see below) and then

    ps> cd bin\
    ps> TwitterConversation.exe <someStatusId>
    
Now the status is downloaded and after you hit Update, the app starts searching for replies.
All the replies are stored in db, so next time they are displayed after you star the app.

In case you would like to see all the stored conversations (up to some limit), just run

    ps> TwitterConversation.exe
    
TwitterClient
-------

This is a very simple Twitter client that displays you only new statuses from friends and mentions.
You can see then only as pictures (default), or after you hit Switch, you see them as full statuses
with text, time, etc.

When you hit Up, you will see previous 15 older statuses, so you can go up the history.

Button clear just clears what is displayed (it means "I saw the statuses, display me only any new one").

SQLite
-------

Both the applications share the same database. So I wouldn't recommend running both
the applications at the same time. This issue just waits for solution.

Build
-------

Open the solution and build the projects. 

There are two F# projects for .NET 4.0. 
Most of the F# source files are common to both of them (and not in a shared project) simply because of slow compiler -- it is better to build bunch of files in one run, then
build it twice.

Registration on Twitter
-------

First time you run any of the applications, your default browser is redirected to Twitter page
and you are asked to confirm that the application can acces your data. Currently only
read access is needed. So you needn't worry ;)

After you confirm mit, Twitter shows you a PIN number. Simply copy and paste the PIN number 
to the application and hit enter.

    ps> TwitterConversation.exe
    Type PIN returned on Twitter page: <here type the PIN>[Enter]
    
And that's all.

License
-------

Most permissive I'll find. I'll add it later.