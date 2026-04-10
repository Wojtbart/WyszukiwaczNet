const historyDal=require('../dataaccesslayer/historyDal');

const getHistory = async (req,res)=>{
     let history = null;
    // try {
    //     const history = await HistoryModel.findAll({
    //         order: [
    //             // will return `name`
    //             ['id','DESC']]
    //     });
        
    // } catch (err) {
    //     console.error('Error fetching user:', err);
    //     throw err;
    // }

    // return history;
   // const { login } = req.params;

    try {
        history = await historyDal.getHistory();

        if (!history) {
            return res.status(404).json({
                success: false,
                message: 'History not found for this username.',
            });
        }

        return res.status(200).json({
            success: true,
            message: `History found`,
            data:history
            //user_id: user.id,
        });
    } catch (error) {
        console.error('Error occurred while fetching user:', error);

        return res.status(500).json({
            success: false,
            message: 'An error occurred while processing the request.',
            error: error.message,
        });
    }
}

module.exports = {
    getHistory
};