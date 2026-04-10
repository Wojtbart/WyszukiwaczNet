const ini = require('ini');
const fs = require('fs');
const config = ini.parse(fs.readFileSync('../config.ini','utf-8'));
const { Client, GatewayIntentBits, EmbedBuilder} = require('discord.js');
const client = new Client({ intents: [GatewayIntentBits.Guilds], partials: ['CHANNEL', 'MESSAGE', 'REACTION'] });

const accountSid = config.tokens.TWILIO_ACCOUNT_SID;
const authToken = config.tokens.TWILIO_AUTH_TOKEN;
const twilio = require('twilio')(accountSid, authToken);

// SMS
async function sendSMStoPhone(textMessage) {
  sentFrom=config.tokens.TWILIO_NUMBER;
  sentTo=config.tokens.PERSONAL_NUMBER; //konto trial umożliwia tylko wysyłanie smsów do siebie

  //TU można było zrobić validację numerów, gdyby konto było płatne
  //  twilio.validationRequests
  // .create({friendlyName: 'My Home Phone Number', phoneNumber: '+48532832332'})
  // .then(validation_request => console.log("aktywuje numer",validation_request.friendlyName));

  twilio.messages.create({
      body: textMessage,
      from: sentFrom,
      to: sentTo
  })
  .then(message =>{
      console.log(`SMS wysłany z ${sentFrom} do ${sentTo}. SID wiadomosci: ${message.sid}`);
  })
  .catch((error) => {
      console.error(error);
  });
}

const sendSms = async (req)=>{

  let textMessage='\n';
  const {data}=req.body;

  if(data.pepper_data){
    for( const [index,item] of  data.pepper_data.entries()){
      if(index>0) break; //tutaj dajemy możliwośc tylko jednego linku
      textMessage+=(item.Tytul+'\n');
      textMessage+=(item.Link+'\n\n');
    }
  }
  if(data.amazon_data){
    for( const [index,item] of  data.amazon_data.entries()){
      if(index>0) break; //tutaj dajemy możliwośc tylko jednego linku
      textMessage+=(item.Tytul+'\n');
      textMessage+=(item.Link+'\n\n');
    }
  }
  if(data.allegro_data){
    for( const [index,item] of  data.allegro_data.entries()){
      if(index>0) break; //tutaj dajemy możliwośc tylko jednego linku
      textMessage+=(item.product_name+'\n');
      textMessage+=(item.price_in_PLN+'\n\n');
    }
  }
  if(data.olx_data){
    for( const [index,item] of data.olx_data.entries()){
      if(index>0) break; //tutaj dajemy możliwośc tylko jednego linku
      textMessage+=(item.Tytul+'\n');
      textMessage+=(item.Link.toString()+'\n\n');
    }
  }

  try {
      sendSMStoPhone(textMessage); 
  } catch (error) {
      console.error(error);
  }
}

// DISCORD
client.on('ready', () => {
    console.log(`Zalogowano jako klient Discorda: ${client.user.tag}!`);
});

const sendToDiscord= async (req) => {

  let {data}=req.body;

  const olxEmbed = new EmbedBuilder()
  .setColor(0x0099FF)
  .setTitle('Oferty sprzedażowe')
  .setAuthor({ name: 'OLX'})
  .setTimestamp()
  .setFooter({ text: 'Oferty OLX' });

  const pepperEmbed = new EmbedBuilder()
  .setColor(0x0099FF)
  .setTitle('Oferty sprzedażowe')
  .setAuthor({ name: 'Pepper'})
  .setTimestamp()
  .setFooter({ text: 'Oferty Pepper' });

  const amazonEmbed = new EmbedBuilder()
  .setColor(0x0099FF)
  .setTitle('Oferty sprzedażowe')
  .setAuthor({ name: 'Amazon'})
  .setTimestamp()
  .setFooter({ text: 'Oferty Amazon' });

  const allegroEmbed = new EmbedBuilder()
  .setColor(0x0099FF)
  .setTitle('Oferty sprzedażowe')
  .setAuthor({ name: 'Allegro'})
  .setTimestamp()
  .setFooter({ text: 'Oferty Allegro' });

  if(data.olx_data){
    for (let i = 0; i < 5; i++) { //5X5 pól=25 to jest maxfield
      if(data.olx_data[i]!=null){
        olxEmbed.addFields({name:"Tytul", value: data.olx_data[i].Tytul})
        olxEmbed.addFields({name:"Link", value: data.olx_data[i].Link, url:data.olx_data[i].Link})
        olxEmbed.addFields({name:"Cena", value: data.olx_data[i].Cena})
        olxEmbed.addFields({name:"Lokalizacja", value: data.olx_data[i].Lokalizacja})
        olxEmbed.addFields({name:"----", value: '-------------------------------------'})
      }
    }
  }

  if(data.allegro_data){
    for (let i = 0; i < 5; i++) { //5X5 pól=25 to jest maxfield
      if(data.allegro_data[i]!=null){
        data.allegro_data[i].product_name !=null ? allegroEmbed.addFields({name:"Tytul", value: (data.allegro_data[i].product_name)}) :allegroEmbed.addFields({name:"Tytul", value: "BRAK"});
        data.allegro_data[i].image_link !=null ? allegroEmbed.addFields({name:"Link do zdjęcia", value: (data.allegro_data[i].image_link), url: data.allegro_data[i].image_link}) :allegroEmbed.addFields({name:"Link do zdjęcia", value: "BRAK"});
        data.allegro_data[i].price_in_PLN !=null ? allegroEmbed.addFields({name:"Cena", value: (data.allegro_data[i].price_in_PLN).toString()}) : allegroEmbed.addFields({name:"Cena", value: "BRAK"});
        data.allegro_data[i].delivery_in_PLN !=null ? allegroEmbed.addFields({name:"Dostawa", value: (data.allegro_data[i].delivery_in_PLN)}) :allegroEmbed.addFields({name:"Dostawa", value: "BRAK"});
        allegroEmbed.addFields({name:"--------", value: '-------------------------------------'})
      }
    }
  }

  if(data.amazon_data){
    for (let i = 0; i < 5; i++) { //5X5 pól=25 to jest maxfield
      if(data.amazon_data[i]!=null){
        data.amazon_data[i].Tytul !=null ? amazonEmbed.addFields({name:"Tytul", value: (data.amazon_data[i].Tytul)}) :amazonEmbed.addFields({name:"Tytul", value: "BRAK"});
        data.amazon_data[i].Link !=null ? amazonEmbed.addFields({name:"Link", value: (data.amazon_data[i].Link), url: data.amazon_data[i].Link}) :amazonEmbed.addFields({name:"Link", value: "BRAK"});
        data.amazon_data[i].Cena_promocyjna !=null ? amazonEmbed.addFields({name:"Cena", value: (data.amazon_data[i].Cena_promocyjna)}) :amazonEmbed.addFields({name:"Cena", value: "BRAK"});
        data.amazon_data[i].Dostawa !=null ? amazonEmbed.addFields({name:"Dostawa", value: (data.amazon_data[i].Dostawa)}) :amazonEmbed.addFields({name:"Dostawa", value: "BRAK"});
        amazonEmbed.addFields({name:"--------", value: '-------------------------------------'})
      }
    }
  }

  if(data.pepper_data){
    for (let i = 0; i < 5; i++) { //5X5 pól=25 to jest maxfield
      if(data.pepper_data[i]!=null){
        data.pepper_data[i].Tytul !=null ? pepperEmbed.addFields({name:"Tytul", value: (data.pepper_data[i].Tytul)}) :pepperEmbed.addFields({name:"Tytul", value: "BRAK"});
        data.pepper_data[i].Link !=null ? pepperEmbed.addFields({name:"Link", value: (data.pepper_data[i].Link), url: data.pepper_data[i].Link}) :pepperEmbed.addFields({name:"Link", value: "BRAK"});
        data.pepper_data[i].Cena_promocyjna !=null ? pepperEmbed.addFields({name:"Cena", value: (data.pepper_data[i].Cena_promocyjna)}) :pepperEmbed.addFields({name:"Cena", value: "BRAK"});
        data.pepper_data[i].Dostawa !=null ? pepperEmbed.addFields({name:"Dostawa", value: (data.pepper_data[i].Dostawa)}) :pepperEmbed.addFields({name:"Dostawa", value: "BRAK"});
        pepperEmbed.addFields({name:"--------", value: '-------------------------------------'})
      }
    }
  }

  const channel = client.channels.cache.get(config.discord.channelId);
  let arrayOfEmbeds=[];
  if (pepperEmbed.data.hasOwnProperty('fields')) arrayOfEmbeds.push(pepperEmbed);
  if (amazonEmbed.data.hasOwnProperty('fields')) arrayOfEmbeds.push(amazonEmbed);
  if (olxEmbed.data.hasOwnProperty('fields')) arrayOfEmbeds.push(olxEmbed);
  if (allegroEmbed.data.hasOwnProperty('fields')) arrayOfEmbeds.push(allegroEmbed);
  
  return await channel.send({ embeds: arrayOfEmbeds })
  .then(message => console.log(`Wysłano wiadomośc do serwisu Discord!`))
  .catch(console.error);
}

const sendMessageToDiscord= (req)=>{
  try{
    sendToDiscord(req);
  }
  catch(err){
    console.error(err);
  } 
}

client.login(config.tokens.BOT_TOKEN);

module.exports={sendSms, sendMessageToDiscord};