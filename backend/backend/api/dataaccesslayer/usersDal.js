const UsersModel=require('../models/UsersModel');
const { Op } = require("sequelize");

const registerUser = async (req,res)=>{
    let usersList={};

    try {
        const {name,surname,email,password,phone,login} =  req.body;

        usersList = await UsersModel.UsersModel.create({
            name: name,
            surname:surname,
            email:email,
            password:password,
            phone:phone,
            login:login
        });

    } catch (err) {
       console.error(err);
    }

    return usersList;
}

const getUser = async (login) => {
    try {
        const user = await UsersModel.UsersModel.findOne({
            where: { login }
        });
        return user;
    } catch (err) {
        console.error('Error fetching user:', err);
        throw err;
    }
}

const getUserConfiguration = async (userId)=>{
    let userConfiguration=null;

    try{ 

        userConfiguration= await UsersModel.Users_configuration_model.findOne({
            where: {
                user_id: userId
            }
        }); 
    }
    catch(err){
        console.error(err);
    } 
    return userConfiguration;
}

const authorizeUserLogin = async (login, password)=>{
    let getUserToLogin=null;

    try{ 

        getUserToLogin = await UsersModel.UsersModel.findOne({
            where: {
                login: login, password:password 
            }
        });
    }
    catch(err){
        console.error(err);
    } 
    return getUserToLogin;
}

const getUsersWithSameValues = async (login, email, phone)=>{
    let getUsers=null;

    try{ 
        getUsers= await UsersModel.UsersModel.findAll({
            where:{
                [Op.or]: [  { login: login }, {email:email},{phone:phone} ]
            }
        });
    }
    catch(err){
        console.error(err);
    } 
    return getUsers;
}

const saveUserConfiguration = async (req)=>{

    const { olx, amazon, allegro, pepper,otoMoto,otoDom, sprzedajemy, sms, discord, email, godzina_maila, repeat_after_specified_time, user_id } = req.body;
    let saveConfiguration=null;

    try{ 
        saveConfiguration = await UsersModel.Users_configuration_model.create({
            olx:olx,
            amazon:amazon,
            allegro:allegro,
            pepper: pepper,
            oto_moto : otoMoto,
            oto_dom : otoDom,
            sprzedajemy : sprzedajemy,
            sms:sms,
            email:email,
            discord:discord,
            godzina_maila:godzina_maila,
            repeat_after_specified_time: repeat_after_specified_time,
            user_id:user_id
        });
    }
    catch(err){
        console.error(err);
    } 
    return saveConfiguration;
}
const updateUserConfiguration = async (req)=>{

    const { olx, amazon, allegro, pepper,otoMoto,otoDom,sprzedajemy, sms, discord, email, godzina_maila, repeat_after_specified_time, user_id } = req.body;
    let updateConfiguration=null;

    try{ 
        updateConfiguration = UsersModel.Users_configuration_model.update({ 
            olx:olx,
            amazon:amazon,
            allegro:allegro,
            pepper: pepper,
            sms:sms,
            email:email,
            discord:discord,
            oto_moto:otoMoto,
            sprzedajemy: sprzedajemy,
            oto_dom:otoDom,
            repeat_after_specified_time: repeat_after_specified_time,
            godzina_maila:godzina_maila
            },
            { 
                where:{
                    user_id: user_id 
                }
            }
        ); 
    }
    catch(err){
        console.error(err);
    } 
    return updateConfiguration;
}

module.exports = {
    registerUser,
    getUser,
    getUserConfiguration,
    authorizeUserLogin,
    getUsersWithSameValues,
    saveUserConfiguration,
    updateUserConfiguration
};