//var id = 0;
//var lookup = {};
// 0 server, 1 local computer, 2 remote computer, 3 local, 4 remote
var lookupName = {};

const WebSocket = require('ws')

const wss = new WebSocket.Server({ port: 8080 },()=>{
    console.log('server started')
})

wss.on('connection', function connection(ws) {
   //ws.id = id++;
   //lookup[ws.id] = ws;
  
   ws.on('message', (data) => {
      if(data[0] === 0x00) {
         console.log('Id received from %o\n', data[1]);
         lookupName[data[1]] = ws;
      } else {
         //console.log('Message for: %s\n', data[0]);
         var destination = data[0];
         var data_sliced = data.slice(1);
         if(destination === 0x05){ // multicast to 3 and 4
            sendToClient(0x03, data_sliced);
            sendToClient(0x04, data_sliced);
         } else {
            sendToClient(destination, data_sliced);
         }
         //console.log('Data[0]: %o\n', data[0])
         //ws.send(data);
      }

   })
   
})

wss.on('listening',()=>{
   console.log('listening on 8080')
})

function sendToClient(id, data){
   if(lookupName[id]) {
      lookupName[id].send(data); // remove data[0]
   } else
   {
      console.log('Client %o not connected', id)
   }

}