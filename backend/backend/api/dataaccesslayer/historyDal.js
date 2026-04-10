const HistoryModel=require('../models/HistoryModel');
const { Op } = require("sequelize");

const getHistory = async (req,res)=>{
    let history = null;

    try {
        
        try {
            history = await HistoryModel.findAll({
                order: [
                    // will return `name`
                    ['id','DESC']]
            });
            
        } catch (err) {
            console.error('Error fetching user:', err);
            throw err;
        }
    
       // return history;
      // const { login } = req.params;

    } catch (err) {
       console.error(err);
    }

    return history;
}

module.exports = {
    getHistory
};