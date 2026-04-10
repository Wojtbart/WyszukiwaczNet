const smsDiscordDal=require('../dataaccesslayer/smsDiscordDal');

const sendSms = async (req)=>{
    
  try {
    const sendSms = await smsDiscordDal.sendSms(req);
    return sendSms;
  } 
  catch (error) {
    return null;
  }
}

const sendMessageToDiscord = async (req)=>{
  try {
    const sendMessageToDiscord = await smsDiscordDal.sendMessageToDiscord(req);
    return sendMessageToDiscord;
  } 
  catch (error) {
    return null;
  }
  
}

module.exports={sendSms, sendMessageToDiscord};