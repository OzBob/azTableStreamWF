# azTableStreamWF
Azure Table of Status Logs streamed to listener on Windows Form

Azure Table called stepperstatus. Fields: uid, eventId, parentEventId?, correlationId, message, level, contextMethod, timestamp, exceptionJson, contextJson

WebAPI 'streamlogging' project: a dotnet 9 minimal API, a REST endpoint that supports POST, GET paged and using HATEOS response, GET(id), POST. A WebAPI GET to the 'subscribe(uid)' endpoint, should open a Socket connection and stream any new log entries until 'unsubscribe(uid)' is called.

Windows Forms 'streamingLogs' component, when click the 'add' button call the streamlogging 'GET' endpoint. When click the 'start' button subscribe to the streamlogging socket connection, display results in the 'logs' textarea. When click the 'stop' button unsubscribe.
