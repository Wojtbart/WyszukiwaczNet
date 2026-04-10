const usersService=require('../servicelayer/users');
const { generateToken } = require('./jwtUtils');

const getUser = async (req, res) => {
    const { login } = req.params;

    try {
        const user = await usersService.getUser(login);

        if (!user) {
            return res.status(404).json({
                success: false,
                message: 'User not found with the provided login.',
            });
        }

        return res.status(200).json({
            success: true,
            message: `User found with login: ${user.login}`,
            user_id: user.id,
        });
    } catch (error) {
        console.error('Error occurred while fetching user:', error);

        return res.status(500).json({
            success: false,
            message: 'An error occurred while processing the request.',
            error: error.message,
        });
    }
};

const getConfiguration = async (req, res) => {
    const { login } = req.params;

     if (!login) {
        return res.status(400).json({
            success: false,
            message: 'Login parameter is required.',
        });
    }

    try{
        const user = await usersService.getUser(login);

        if (!user) {
            return res.status(404).json({
                success: false,
                message: `User not found for login: ${login}`,
            });
        }

        const userConfiguration = await usersService.getUserConfiguration(user.id);

        if (!userConfiguration) {
            return res.status(404).json({
                success: false,
                message: 'No configuration found for this user.',
            });
        }

        return res.status(200).json({
            success: true,
            message: 'User configuration retrieved successfully.',
            data: userConfiguration,
        });
    }
    catch (error) {
        console.error('Error fetching user configuration:', error);

        return res.status(500).json({
            success: false,
            message: 'An unexpected error occurred while fetching user configuration.',
            error: error.message,
        });
    }
}

const login = async (req, res) => {
    const { login, password } = req.body;

    if (!login || !password) {
        return res.status(400).json({
            success: false,
            message: 'Login and password are required.',
        });
    }

    try {
        const user = await usersService.authorizeUserLogin(login, password);

        if (!user) {
            return res.status(401).json({
                success: false,
                message: 'Invalid login credentials.',
            });
        }

        const token = generateToken({
            id: user.id, 
            login: user.login,
        });

        return res.status(200).json({
            success: true,
            message: 'Login successful.',
            token: token,
        });
    } 
    catch (error) {
        console.error('Login error:', error);

        return res.status(500).json({
            success: false,
            message: 'An unexpected error occurred!!!',
            error: error.message, 
        });
    }
};

const registerUser = async (req, res) => {
    const { email, phone, login, password, name, surname } = req.body;
    
    if (!login || !phone || !email || !password || !name || !surname) {
        return res.status(400).json({
            success: false,
            message: 'Login, password, name, surname,  phone and email are required!!!',
        });
    }

    try {
        const users = await usersService.getUsersWithSameValues(login, email, phone);

        if (users && users.length > 0) {
            const user = users[0]; 
            let errorMessage = '';

            if (user.email === email) errorMessage = 'A user with this email already exists.';
            else if (user.login === login) errorMessage = 'A user with this login already exists.';
            else if (user.phone === phone) errorMessage = 'A user with this phone number already exists.';

            return res.status(409).json({
                success: false,
                message: "Registration failed.",
                error: errorMessage
            });
        }

        await usersService.registerUser(req, res); 

        return res.status(201).json({
            success: true,
            message: 'User successfully registered.'
        });
    
    } 
    catch (err) {
        console.error('Error during user registration:', err);

        return res.status(500).json({
            success: false,
            message: "An error occurred during user registration.",
            error: err.message 
        });
    }
};
  
const saveConfiguration = async (req, res) => {
    const { user_id } = req.body;

    if (!user_id) {
        return res.status(400).json({
            success: false,
            message: 'User ID is required!',
        });
    }

    try {
        const existingConfig = await usersService.getUserConfiguration(user_id);
        let savedConfiguration = null;

        if (existingConfig) {
            const updatedConfig = await usersService.updateUserConfiguration(req);

            if (!updatedConfig) {
                return res.status(500).json({
                    success: false,
                    message: 'Error updating user configuration.',
                });
            }

            savedConfiguration = updatedConfig;
        } else {
            const newConfig = await usersService.saveUserConfiguration(req);

            if (!newConfig) {
                return res.status(500).json({
                    success: false,
                    message: 'Error creating user configuration.',
                });
            }

            savedConfiguration = newConfig;
        }

        return res.status(200).json({
            success: true,
            message: 'Configuration saved successfully.'
        });

    } catch (error) {
        console.error('Error saving configuration:', error);

        return res.status(500).json({
            success: false,
            message: 'An unexpected error occurred.',
            error: error.message,
        });
    }
};

module.exports = {
    registerUser,
    getUser,
    login,
    saveConfiguration,
    getConfiguration,
};