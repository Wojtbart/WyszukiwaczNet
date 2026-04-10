const usersDal=require('../dataaccesslayer/usersDal');

const registerUser = async (req,res)=>{
    
    const registeredUser = await usersDal.registerUser(req,res);
    return (registeredUser);
}

const getUser = async (login)=>{
    
    const getUsers = await usersDal.getUser(login);
    return (getUsers);
}

const getUserConfiguration = async (userId)=>{
    
    const getUsersConfiguration = await usersDal.getUserConfiguration(userId);
    return (getUsersConfiguration);
}

const authorizeUserLogin = async (login,password)=>{
    
    const getUsers = await usersDal.authorizeUserLogin(login,password);
    return (getUsers);
}

const getUsersWithSameValues = async (login,email,phone)=>{
    
    const getUsers = await usersDal.getUsersWithSameValues(login,email,phone);
    return (getUsers);
}
const saveUserConfiguration = async (req)=>{
    
    const saveConfiguration = await usersDal.saveUserConfiguration(req);
    return (saveConfiguration);
}
const updateUserConfiguration = async (req)=>{
    
    const updateConfiguration = await usersDal.updateUserConfiguration(req);
    return (updateConfiguration);
}

module.exports={registerUser, getUser, getUserConfiguration, authorizeUserLogin, getUsersWithSameValues, saveUserConfiguration, updateUserConfiguration};